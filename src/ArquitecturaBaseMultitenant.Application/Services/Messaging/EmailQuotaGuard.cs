using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Services.Messaging;

/// <summary>
/// Los topes de lo que sale por correo con un código o una invitación (ADR 0063 y decisión 256), en las 24 horas móviles de
/// <see cref="MessageQuotaGuard"/>, dentro de la transacción del llamador y después de sus locks:
/// <list type="bullet">
/// <item>por dirección, todo código por correo que se emite desde la del pedido (una IPv6, por su /64), salga o no: si
/// contara solo los que salen, el tope diría qué cuentas están bloqueadas o suspendidas;</item>
/// <item>por cuenta, los códigos por correo que pide desde Mi cuenta;</item>
/// <item>por persona que invita (también un operador) y por organización, las invitaciones por correo;</item>
/// <item>el global, lo que sale por correo con un código o una invitación. Los códigos frenan en el tope menos la reserva
/// de las invitaciones, y las invitaciones llegan al tope: un abuso de los códigos no deja a nadie sin invitar.</item>
/// </list>
/// Un código que no sale suma solo en su dirección, pero mira sin sumar los demás topes de los códigos: con los lugares de
/// los códigos llenos responde el mismo 429 que uno que sale, así el global tampoco dice qué cuentas están bloqueadas. Con
/// dos topes llenos, la espera es la del que libera lugar más tarde.
/// <para>
/// Al pasar el 80 % del global, al frenar los códigos y al llegar al tope queda un aviso en el log, después del commit
/// (<see cref="IAfterCommit"/>): cada aviso lo deja el pedido que cruza esa marca y se guarda, que es uno solo entre todas
/// las copias porque suma con el lock del global; si se deshace, la cruza el siguiente. El rechazo es
/// <see cref="EmailQuotaErrors.DailyLimit"/> con la espera; no suma en ningún contador. Es la admisión del canal de correo:
/// <see cref="Services.Auth.LoginCodeIssuer"/> la llama al emitir, el emisor de las invitaciones de una organización por
/// <see cref="IInvitationAdmission"/>, y la invitación de un operador llama a <see cref="ReserveInvitationAsync"/>.
/// </para>
/// </summary>
internal sealed partial class EmailQuotaGuard(MessageQuotaGuard quotas, IMessageQuotaKeyHasher keys, IRequestInfo request,
    IOptions<EmailQuotaOptions> options, IAfterCommit afterCommit, ILogger<EmailQuotaGuard> logger)
    : ILoginCodeAdmission, IInvitationAdmission
{
    private const decimal WarningShare = 0.8m;

    public string Channel => LoginCodeChannel.Email;

    string IInvitationAdmission.Channel => InvitationChannel.Email;

    Task<Result> IInvitationAdmission.ReserveAsync(Guid inviterId, Guid organizationId, CancellationToken cancellationToken) =>
        ReserveInvitationAsync(inviterId, organizationId, cancellationToken);

    /// <summary>
    /// Un código por correo que sale: el tope de la dirección, el de la cuenta si lo pide desde Mi cuenta y el global hasta
    /// la reserva de las invitaciones.
    /// </summary>
    public Task<Result> ReserveAsync(LoginCodeDestination destination, LoginCodePurpose purpose, Guid? ownerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return ReserveAsync(CodeLimits(purpose, ownerId, sent: true), cancellationToken);
    }

    /// <summary>
    /// Un código por correo que se emite y no sale: suma en su dirección, y la cuenta y el global se miran sin sumar, así
    /// responde lo mismo que uno que sale.
    /// </summary>
    public Task<Result> ReserveWithheldAsync(LoginCodeDestination destination, LoginCodePurpose purpose, Guid? ownerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return ReserveAsync(CodeLimits(purpose, ownerId, sent: false), cancellationToken);
    }

    /// <summary>
    /// Una invitación por correo de <paramref name="inviterId"/>: su tope, el de la organización (sin organización, como la
    /// de un operador, no hay) y el global hasta el tope entero.
    /// </summary>
    public Task<Result> ReserveInvitationAsync(Guid inviterId, Guid? organizationId, CancellationToken cancellationToken)
    {
        if (inviterId == Guid.Empty) throw new ArgumentException("The inviter is required.", nameof(inviterId));
        var settings = options.Value;
        List<MessageQuotaLimit> limits =
        [
            new(new(MessageQuotaScope.Inviter, keys.ForSubject(MessageQuotaScope.Inviter, inviterId)),
                settings.DailyInvitationLimitPerInviter),
        ];
        if (organizationId is { } tenantId)
            limits.Add(new(new(MessageQuotaScope.Organization, keys.ForSubject(MessageQuotaScope.Organization, tenantId)),
                settings.DailyInvitationLimitPerOrganization));
        limits.Add(new(MessageQuotaKey.Global, settings.DailyLimit));
        return ReserveAsync(limits, cancellationToken);
    }

    // La dirección suma siempre; la cuenta y el global, solo si el código sale.
    private List<MessageQuotaLimit> CodeLimits(LoginCodePurpose purpose, Guid? ownerId, bool sent)
    {
        var settings = options.Value;
        List<MessageQuotaLimit> limits =
        [
            new(new(MessageQuotaScope.CodeIp, keys.ForAddress(request.IpAddress)), settings.DailyCodeLimitPerAddress),
        ];
        if (purpose == LoginCodePurpose.VerifyDestination && ownerId is { } accountId)
            limits.Add(new(new(MessageQuotaScope.CodeAccount, keys.ForSubject(MessageQuotaScope.CodeAccount, accountId)),
                settings.DailyCodeLimitPerAccount, sent));
        limits.Add(new(MessageQuotaKey.Global, settings.DailyCodeLimit, sent));
        return limits;
    }

    private async Task<Result> ReserveAsync(List<MessageQuotaLimit> limits, CancellationToken ct)
    {
        var check = await quotas.CheckAsync(limits, ct);
        if (check.Exceeded is { } full)
        {
            LogLimitReached(logger, full.Key.Scope, full.Limit);
            return EmailQuotaErrors.DailyLimit(check.RetryAfterSeconds);
        }
        await quotas.RecordAsync(check, ct);
        for (var index = 0; index < limits.Count; index++)
            if (limits[index] is { Records: true, Key.Scope: MessageQuotaScope.Global }) Warn(check.Counts[index] + 1);
        return Result.Success();
    }

    // Cada marca la cruza un solo pedido: el global suma de a uno, con su lock. El aviso va con el commit.
    private void Warn(int sent)
    {
        var settings = options.Value;
        var (limit, codeLimit) = (settings.DailyLimit, settings.DailyCodeLimit);
        if (sent == limit) afterCommit.Add(() => LogDailyLimitReached(logger, limit));
        else if (sent == codeLimit && settings.DailyInvitationReserve > 0)
            afterCommit.Add(() => LogCodesStopped(logger, sent, limit));
        if (sent == (int)Math.Ceiling(limit * WarningShare) && sent < limit)
            afterCommit.Add(() => LogDailyLimitNear(logger, sent, limit, codeLimit));
    }

    // Sin la dirección, la cuenta, quien invita ni la organización: solo qué tope.
    [LoggerMessage(Level = LogLevel.Information,
        Message = "An email limit of {Scope} reached {Limit} in the last 24 hours; nothing is sent or saved for it until its oldest hour leaves the window")]
    private static partial void LogLimitReached(ILogger logger, MessageQuotaScope scope, int limit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "The emails with a code or an invitation of the last 24 hours reached {Sent} of the limit of {Limit}; codes by email stop at {CodeLimit} and invitations at the limit")]
    private static partial void LogDailyLimitNear(ILogger logger, int sent, int limit, int codeLimit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "The emails with a code or an invitation of the last 24 hours reached {Sent}: codes by email stop until the oldest hour leaves the window, and invitations go on up to {Limit}")]
    private static partial void LogCodesStopped(ILogger logger, int sent, int limit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "The emails with a code or an invitation of the last 24 hours reached the limit of {Limit}; no code or invitation is sent by email until the oldest hour leaves the window")]
    private static partial void LogDailyLimitReached(ILogger logger, int limit);
}
