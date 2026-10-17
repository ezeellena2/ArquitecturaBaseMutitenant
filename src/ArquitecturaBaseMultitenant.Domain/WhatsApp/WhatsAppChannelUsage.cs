using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Contador técnico del mes de un número (decisión 8 de la E11): mensajes de servicio entregados y los que Meta ya cobra,
/// y en Plataforma los de códigos y avisos. Lo escriben los webhooks de estado; no es una configuración.
/// </summary>
public sealed class WhatsAppChannelUsage : Entity
{
    private WhatsAppChannelUsage() { }
    public Guid ChannelId { get; private set; }
    public DateOnly Month { get; private set; }
    public int ServiceDelivered { get; private set; }
    public int ServiceCharged { get; private set; }
    public int OtherDelivered { get; private set; }

    public static WhatsAppChannelUsage Create(Guid channelId, DateOnly month)
    {
        if (channelId == Guid.Empty) throw new ArgumentException("A channel is required.", nameof(channelId));
        if (month.Day != 1) throw new ArgumentException("The month starts on its first day.", nameof(month));
        return new WhatsAppChannelUsage { ChannelId = channelId, Month = month };
    }

    /// <summary>Un mensaje entregado, con la categoría y el tipo de precio que informó Meta.</summary>
    public void RecordDelivered(string? category, string? pricingType)
    {
        if (string.Equals(category, "service", StringComparison.OrdinalIgnoreCase))
        {
            ServiceDelivered++;
            if (string.Equals(pricingType, "regular", StringComparison.OrdinalIgnoreCase)) ServiceCharged++;
        }
        else OtherDelivered++;
    }
}
