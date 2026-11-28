using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Configuration;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Los topes diarios de las invitaciones por WhatsApp (decisión 1 de los arreglos de la revisión), en las 24 horas móviles de
/// <see cref="MessageQuotaGuard"/>, dentro de la transacción del llamador y después de sus locks: uno por organización y el
/// global del número de Plataforma. Los cuenta la carga de «Cargar teléfonos», que reserva su lote entero, y cada reenvío
/// desde Usuarios o desde una empresa, que llega por <see cref="IInvitationAdmission"/> al emitir la invitación (B9).
/// <list type="bullet">
/// <item>Si el lote es más grande que el tope (el menor entre el de la organización y el global), no entra ni con la ventana
/// vacía: <see cref="WhatsAppErrors.PhoneImportTooManyInvitationsCode"/>, sin espera y con el máximo, para partirlo.</item>
/// <item>Si entra en el tope pero no en lo que queda del día, 429 <see cref="WhatsAppErrors.PhoneImportDailyLimitCode"/> con
/// la espera en <c>retryAfter</c>.</item>
/// </list>
/// Un rechazo no suma nada. Al cruzar el 80 % o llegar al 100 % del global queda un aviso en el log después del commit
/// (<see cref="IAfterCommit"/>).
/// </summary>
internal sealed partial class WhatsAppPhoneImportQuotaGuard(MessageQuotaGuard quotas, IMessageQuotaKeyHasher keys,
    IOptions<WhatsAppQuotaOptions> options, IAfterCommit afterCommit, ILogger<WhatsAppPhoneImportQuotaGuard> logger)
    : IInvitationAdmission
{
    private const decimal WarningShare = 0.8m;

    private static readonly MessageQuotaKey GlobalKey =
        new(MessageQuotaScope.WhatsAppInvitationGlobal, MessageQuotaKey.GlobalKey);

    public string Channel => WhatsAppInvitationChannel.ChannelKey;

    /// <summary>Una invitación por WhatsApp que se emite de a una (un reenvío): cuenta uno en los mismos topes.</summary>
    public Task<Result> ReserveAsync(Guid inviterId, Guid organizationId, CancellationToken cancellationToken) =>
        ReserveAsync(organizationId, 1, cancellationToken);

    /// <summary>
    /// Sin tocar la base: si un lote de <paramref name="count"/> invitaciones cabe en el tope diario con la ventana vacía.
    /// La carga lo mira antes de abrir su transacción, así una lista demasiado grande no toma ningún lock.
    /// </summary>
    internal Result CheckBatchSize(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var settings = options.Value;
        var dailyLimit = Math.Min(settings.DailyInvitationLimitPerOrganization, settings.DailyInvitationLimit);
        if (count <= dailyLimit) return Result.Success();
        LogBatchTooLarge(logger, count, dailyLimit);
        return WhatsAppErrors.PhoneImportTooManyInvitations(dailyLimit);
    }

    /// <summary>
    /// Reserva lugar para un lote entero de <paramref name="count"/> invitaciones de <paramref name="organizationId"/>.
    /// Si <paramref name="count"/> es 0, no toca contadores ni toma locks; si no entra ni con la ventana vacía, tampoco.
    /// </summary>
    internal async Task<Result> ReserveAsync(Guid organizationId, int count, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("The organization is required.", nameof(organizationId));
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0) return Result.Success();
        var size = CheckBatchSize(count);
        if (size.IsFailure) return size;

        var settings = options.Value;
        MessageQuotaLimit[] limits =
        [
            new(new(MessageQuotaScope.WhatsAppInvitationOrganization,
                keys.ForSubject(MessageQuotaScope.WhatsAppInvitationOrganization, organizationId)),
                settings.DailyInvitationLimitPerOrganization),
            new(GlobalKey, settings.DailyInvitationLimit),
        ];

        var check = await quotas.CheckAsync(limits, count, cancellationToken);
        if (check.Exceeded is { } full)
        {
            LogLimitReached(logger, full.Key.Scope, full.Limit, count);
            return WhatsAppErrors.PhoneImportDailyLimit(check.RetryAfterSeconds);
        }

        await quotas.RecordAsync(check, cancellationToken);
        Warn(check.Counts[1], check.Counts[1] + count, settings.DailyInvitationLimit);
        return Result.Success();
    }

    // Cada marca la cruza un solo pedido: el global suma con su lock. El aviso va con el commit.
    private void Warn(int previous, int sent, int limit)
    {
        if (previous < limit && sent >= limit)
        {
            afterCommit.Add(() => LogDailyLimitReached(logger, limit));
            return;
        }

        var warningAt = (int)Math.Ceiling(limit * WarningShare);
        if (previous < warningAt && sent >= warningAt && sent < limit)
            afterCommit.Add(() => LogDailyLimitNear(logger, sent, limit));
    }

    // Sin la organización ni teléfonos: solo qué tope y cuántos pidió la carga.
    [LoggerMessage(Level = LogLevel.Information,
        Message = "A WhatsApp invitation limit of {Scope} reached {Limit} in the last 24 hours for a batch of {Requested}; nothing is sent or saved until enough hours leave the window")]
    private static partial void LogLimitReached(ILogger logger, MessageQuotaScope scope, int limit, int requested);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "A batch of {Requested} WhatsApp invitations is larger than the daily limit of {Limit}; nothing is sent or saved and it has to be split")]
    private static partial void LogBatchTooLarge(ILogger logger, int requested, int limit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "WhatsApp invitations of the last 24 hours reached {Sent} of the global limit of {Limit}")]
    private static partial void LogDailyLimitNear(ILogger logger, int sent, int limit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "WhatsApp invitations of the last 24 hours reached the global limit of {Limit}; no more WhatsApp invitations are sent until the oldest hour leaves the window")]
    private static partial void LogDailyLimitReached(ILogger logger, int limit);
}
