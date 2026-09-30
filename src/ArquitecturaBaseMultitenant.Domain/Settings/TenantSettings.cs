using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>Guarda los valores predeterminados de presentación de un espacio; Application los elige desde los catálogos habilitados.</summary>
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

    public static TenantSettings Create(string defaultCulture, string defaultTimeZoneId,
        string defaultCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCulture);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTimeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);

        return new TenantSettings
        {
            DefaultCulture = defaultCulture,
            DefaultTimeZoneId = defaultTimeZoneId,
            DefaultCurrency = defaultCurrency,
        };
    }
}
