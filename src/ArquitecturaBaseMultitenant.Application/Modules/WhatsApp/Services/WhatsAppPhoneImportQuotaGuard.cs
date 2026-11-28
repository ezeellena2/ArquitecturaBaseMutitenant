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
/// Los topes diarios de las invitaciones por WhatsApp de «Cargar teléfonos» (decisión 1 de los arreglos de la revisión),
/// en las 24 horas móviles de <see cref="MessageQuotaGuard"/>, dentro de la transacción del llamador y después de sus locks:
/// uno por organización y el global del número de Plataforma. Si la carga entera no entra en lo que queda del día, responde
/// 429 <see cref="WhatsAppErrors.PhoneImportDailyLimitCode"/> con la espera en <c>retryAfter</c> y no suma nada. Al cruzar
/// el 80 % o llegar al 100 % del global queda un aviso en el log después del commit (<see cref="IAfterCommit"/>).
/// </summary>
internal sealed partial class WhatsAppPhoneImportQuotaGuard(MessageQuotaGuard quotas, IMessageQuotaKeyHasher keys,
    IOptions<WhatsAppQuotaOptions> options, IAfterCommit afterCommit, ILogger<WhatsAppPhoneImportQuotaGuard> logger)
{
    private const decimal WarningShare = 0.8m;

    private static readonly MessageQuotaKey GlobalKey =
        new(MessageQuotaScope.WhatsAppInvitationGlobal, MessageQuotaKey.GlobalKey);

    /// <summary>
    /// Reserva lugar para un lote entero de <paramref name="count"/> invitaciones de <paramref name="organizationId"/>.
    /// Si <paramref name="count"/> es 0, no toca contadores ni toma locks.
    /// </summary>
    internal async Task<Result> ReserveAsync(Guid organizationId, int count, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("The organization is required.", nameof(organizationId));
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0) return Result.Success();

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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "WhatsApp invitations of the last 24 hours reached {Sent} of the global limit of {Limit}")]
    private static partial void LogDailyLimitNear(ILogger logger, int sent, int limit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "WhatsApp invitations of the last 24 hours reached the global limit of {Limit}; no more phone import invitations are sent until the oldest hour leaves the window")]
    private static partial void LogDailyLimitReached(ILogger logger, int limit);
}
