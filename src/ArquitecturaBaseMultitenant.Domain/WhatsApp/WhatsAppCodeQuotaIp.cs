using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Contador técnico del día, sin IP en claro ni vínculo a una persona.</summary>
public sealed class WhatsAppCodeQuotaIp : Entity
{
    private WhatsAppCodeQuotaIp() { }
    public Guid QuotaId { get; private set; }
    public string IpKey { get; private set; } = string.Empty;
    public int ReservedCount { get; private set; }
    public static WhatsAppCodeQuotaIp Create(Guid quotaId, string ipKey)
    {
        if (quotaId == Guid.Empty) throw new ArgumentException("The quota is required.", nameof(quotaId));
        ArgumentException.ThrowIfNullOrWhiteSpace(ipKey);
        return new WhatsAppCodeQuotaIp { QuotaId = quotaId, IpKey = ipKey };
    }
    public void Reserve() => ReservedCount++;
}
