using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Features;

/// <summary>Habilitación global de un módulo para un espacio, con vencimiento opcional y versión.</summary>
public sealed class TenantFeature : Entity, IAuditable, IVersioned
{
    private TenantFeature() { }

    public Guid TenantId { get; private set; }
    public string FeatureKey { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public TenantFeatureSource Source { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public uint Version { get; private set; }

    public static Result<TenantFeature> Create(Guid tenantId, string featureKey, bool enabled,
        TenantFeatureSource source, DateTime? expiresAtUtc, DateOnly? expiresOn = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("The organization id cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(featureKey) || featureKey.Length > TextLimits.ShortName)
            return TenantFeatureErrors.InvalidKey;
        var feature = new TenantFeature { TenantId = tenantId, FeatureKey = featureKey };
        var update = feature.Update(enabled, source, expiresAtUtc, expiresOn);
        return update.IsFailure ? update.Error : feature;
    }

    public Result Update(bool enabled, TenantFeatureSource source, DateTime? expiresAtUtc, DateOnly? expiresOn = null)
    {
        if (expiresAtUtc is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("The expiry instant must be UTC.", nameof(expiresAtUtc));
        if (!Enum.IsDefined(source)) return TenantFeatureErrors.InvalidSource;
        if (source == TenantFeatureSource.Trial && expiresAtUtc is null)
            return TenantFeatureErrors.ExpiryRequired;
        Enabled = enabled;
        Source = source;
        ExpiresAtUtc = expiresAtUtc;
        ExpiresOn = expiresOn;
        return Result.Success();
    }

    public bool IsEnabledAt(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        return Enabled && (ExpiresAtUtc is null || nowUtc < ExpiresAtUtc);
    }
}
