using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformFeatureWriter(ITenantReader tenants, ITenantFeatureRepository features,
    IFeatureCatalog catalog, PlatformAuditWriter audit, ITenantSettingsRepository settings, ITimeZoneService timeZones)
{
    internal async Task<Result<TenantFeature>> UpdateAsync(Guid tenantId, string key, UpdatePlatformFeatureRequest request,
        CancellationToken ct)
    {
        // El índice admite una fila por origen (ADR 0069): el lock, antes de leer, mantiene una sola fila por módulo (0060),
        // y la segunda alta a la vez lee la de la primera y responde 409 por versión. Solo para un módulo del catálogo.
        var definition = catalog.All.SingleOrDefault(row => row.Key == key);
        if (definition is not null) await features.LockAsync(tenantId, definition.Key, ct);
        var tenant = await tenants.FindByIdAsync(tenantId, ct);
        if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
        if (definition is null) return TenantFeatureErrors.NotFound;
        if (!definition.Supports(tenant.Kind)) return TenantFeatureErrors.InvalidScope;
        var feature = await features.GetForUpdateAsync(tenantId, key, request.Version!.Value, ct);
        DateTime? expiresAtUtc = null;
        if (request.ExpiresOn is { } expiresOn)
        {
            var preferences = await settings.GetCurrentAsync(ct);
            if (preferences is null) return SettingsErrors.TenantNotFound;
            expiresAtUtc = timeZones.GetDayRangeUtc(expiresOn, preferences.DefaultTimeZoneId).EndUtc;
        }
        var changes = new SecurityEventChanges();
        var prefix = "features." + definition.Key + ".";
        changes.AddBoolean(prefix + "enabled", feature?.Enabled ?? definition.DefaultEnabled, request.Enabled);
        changes.AddEnum<TenantFeatureSource>(prefix + "source", feature?.Source, request.DomainSource);
        changes.AddUtc(prefix + "expiresAtUtc", feature?.ExpiresAtUtc, expiresAtUtc);
        if (feature is null)
        {
            if (request.Version.Value != 0) return TenantFeatureErrors.NotFound;
            var created = TenantFeature.Create(tenantId, key, request.Enabled!.Value, request.DomainSource!.Value, expiresAtUtc, request.ExpiresOn);
            if (created.IsFailure) return created.Error;
            feature = created.Value;
            features.Add(feature);
        }
        else
        {
            var update = feature.Update(request.Enabled!.Value, request.DomainSource!.Value, expiresAtUtc, request.ExpiresOn);
            if (update.IsFailure) return update.Error;
            features.MarkUpdated(feature);
        }
        audit.RecordFeatures(tenantId, request.Reason!, changes);
        return feature;
    }
}
