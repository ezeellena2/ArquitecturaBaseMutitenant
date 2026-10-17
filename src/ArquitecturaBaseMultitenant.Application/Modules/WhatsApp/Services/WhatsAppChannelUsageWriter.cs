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
    ITimeZoneService zones, TimeProvider clock)
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
    internal async Task<IReadOnlySet<string>> LockActiveAsync(IEnumerable<WhatsAppWebhookStatus> statuses, CancellationToken ct)
    {
        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (var number in statuses.Select(value => value.PhoneNumberId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            await channels.LockNumberAsync(number, ct);
            if (await channels.GetActiveAsync(number, ct) is not null) active.Add(number);
        }
        return active;
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
