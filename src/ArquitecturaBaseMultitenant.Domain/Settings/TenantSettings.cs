using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Settings;

public sealed class TenantSettings : Entity, ITenantOwned, IAuditable
{
    private TenantSettings() { }

    public Guid TenantId { get; private set; }
    public string DefaultCulture { get; private set; } = string.Empty;
    public string DefaultTimeZoneId { get; private set; } = string.Empty;
    public string DefaultCurrency { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    public static TenantSettings Create(Guid tenantId, string defaultCulture, string defaultTimeZoneId,
        string defaultCurrency)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("The tenant id cannot be empty.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCulture);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTimeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);

        return new TenantSettings
        {
            TenantId = tenantId,
            DefaultCulture = defaultCulture,
            DefaultTimeZoneId = defaultTimeZoneId,
            DefaultCurrency = defaultCurrency,
        };
    }
}
