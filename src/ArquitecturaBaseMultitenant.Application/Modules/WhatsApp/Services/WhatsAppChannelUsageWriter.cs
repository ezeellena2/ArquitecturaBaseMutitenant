using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Lo que un estado de entrega le cambia a su número, dentro de la transacción del webhook: el contador del mes (decisión 8,
/// una vez por mensaje entregado) y la falta de medio de pago que informa Meta con el error 131042.
/// </summary>
internal sealed class WhatsAppChannelUsageWriter(IWhatsAppChannelRepository channels, WhatsAppChannelAuditWriter audit,
    ITimeZoneService zones, IWhatsAppChatRepository chats, IPlatformWhatsAppChatRepository platformChats,
    IWhatsAppBroadcastRepository broadcasts, TimeProvider clock)
{
    internal const int PaymentRequiredError = 131042;

    /// <summary>Los números vigentes de la entrada, sin lock: un número que se desconecta en el medio solo pierde ese mensaje.</summary>
    internal async Task<IReadOnlyDictionary<string, WhatsAppChannel>> FindActiveAsync(IEnumerable<string> numbers, CancellationToken ct)
    {
        var active = new Dictionary<string, WhatsAppChannel>(StringComparer.Ordinal);
        foreach (var number in numbers.Distinct(StringComparer.Ordinal))
            if (await channels.GetActiveAsync(number, ct) is { } channel) active.Add(number, channel);
        return active;
    }

    /// <summary>Serializa los números de los estados del lote, después de los locks de cada mensaje, y devuelve los vigentes.</summary>
    internal async Task<IReadOnlyDictionary<string, WhatsAppChannel>> LockActiveAsync(IEnumerable<WhatsAppWebhookStatus> statuses, CancellationToken ct)
    {
        var active = new Dictionary<string, WhatsAppChannel>(StringComparer.Ordinal);
        foreach (var number in statuses.Select(value => value.PhoneNumberId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            await channels.LockNumberAsync(number, ct);
            if (await channels.GetActiveAsync(number, ct) is { } channel) active.Add(number, channel);
        }
        return active;
    }

    /// <summary>Anota el motivo de un <c>status: failed</c> en un mensaje saliente del número de Plataforma.</summary>
    internal async Task RecordPlatformDeliveryFailureAsync(Guid outboxMessageId, string wamid, string failureReason, CancellationToken ct)
    {
        if (await platformChats.GetOutboundAsync(outboxMessageId, ct) is { } message)
            message.ApplyDelivery(WhatsAppChatMessageStatus.Failed, wamid, failureReason);
    }

    /// <summary>
    /// Anota el motivo de cada <c>status: failed</c> de una organización en su mensaje de chat o destinatario de difusión,
    /// dentro del alcance de esa organización.
    /// </summary>
    internal async Task RecordOrganizationDeliveryFailuresAsync(IReadOnlyList<WhatsAppDeliveryFailure> failures, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        foreach (var failure in failures)
        {
            if (await chats.GetOutboundAsync(failure.OutboxMessageId, ct) is { } message)
                message.ApplyDelivery(WhatsAppChatMessageStatus.Failed, failure.Wamid, failure.FailureReason);
            if (await broadcasts.GetByOutboxMessageAsync(failure.OutboxMessageId, ct) is { } recipient)
                recipient.ApplyDeliveryFailure(failure.FailureReason, failure.OccurredAtUtc > nowUtc ? nowUtc : failure.OccurredAtUtc);
        }
    }

    internal async Task RecordAsync(WhatsAppWebhookStatus status, bool firstDelivery, CancellationToken ct)
    {
        var paymentMissing = status is { Status: WhatsAppMessageStatus.Failed, ErrorCode: PaymentRequiredError };
        if (!firstDelivery && !paymentMissing) return;
        var channel = await channels.GetActiveAsync(status.PhoneNumberId, ct);
        if (channel is null) return;
        if (paymentMissing)
        {
            var before = channel.Status;
            if (channel.ApplyPayment(false))
            {
                channels.MarkUpdated(channel);
                audit.Record(channel, "WhatsApp number without payment method (Meta 131042)",
                    WhatsAppChannelAuditWriter.Status(before, channel.Status), bySystem: true);
            }
        }
        if (!firstDelivery) return;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var deliveredAtUtc = status.OccurredAtUtc > nowUtc ? nowUtc : status.OccurredAtUtc;
        var month = WhatsAppChannel.UsageMonth(zones.ConvertToLocal(deliveredAtUtc, channel.UsageTimeZoneId));
        var usage = await channels.GetUsageAsync(channel.Id, month, ct);
        if (usage is null)
        {
            usage = WhatsAppChannelUsage.Create(channel.Id, month);
            channels.Add(usage);
        }
        usage.RecordDelivered(status.PricingCategory, status.PricingType);
    }
}
