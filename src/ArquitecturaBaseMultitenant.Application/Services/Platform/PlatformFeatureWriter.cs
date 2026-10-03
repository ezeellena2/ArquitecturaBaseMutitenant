using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformFeatureWriter(ITenantReader tenants, ITenantFeatureRepository features,
    IFeatureCatalog catalog, PlatformTenantAudit audit)
{
    internal async Task<Result> UpdateAsync(Guid tenantId, string key, UpdatePlatformFeatureRequest request, CancellationToken ct)
    {
        var tenant = await tenants.FindByIdAsync(tenantId, ct);
        if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
        var definition = catalog.All.SingleOrDefault(row => row.Key == key);
        if (definition is null) return TenantFeatureErrors.NotFound;
        if (!definition.Supports(tenant.Kind)) return TenantFeatureErrors.InvalidScope;
        var feature = await features.GetForUpdateAsync(tenantId, key, request.Version!.Value, ct);
        if (feature is null)
        {
            if (request.Version.Value != 0) return TenantFeatureErrors.NotFound;
            var created = TenantFeature.Create(tenantId, key, request.Enabled!.Value, request.Source!.Value, request.ExpiresAtUtc);
            if (created.IsFailure) return created.Error;
            features.Add(created.Value);
        }
        else
        {
            var update = feature.Update(request.Enabled!.Value, request.Source!.Value, request.ExpiresAtUtc);
            if (update.IsFailure) return update;
            features.MarkUpdated(feature);
        }
        audit.RecordFeatures(tenantId, request.Reason!);
        return Result.Success();
    }
}
