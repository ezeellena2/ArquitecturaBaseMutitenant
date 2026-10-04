using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformFeatureListBuilder(ITenantReader tenants, ITenantFeatureReader features, IFeatureCatalog catalog)
{
    internal async Task<Result<IReadOnlyList<PlatformFeatureResponse>>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await tenants.FindByIdAsync(tenantId, ct);
        if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
        var grants = await features.ListAsync(tenantId, ct);
        return catalog.All.Where(definition => definition.Supports(tenant.Kind)).Select(definition =>
        {
            var grant = grants.SingleOrDefault(row => row.Key == definition.Key);
            return new PlatformFeatureResponse(definition.Key, grant?.Enabled ?? definition.DefaultEnabled,
                grant?.Source ?? TenantFeatureSource.Plan, grant?.ExpiresAtUtc, grant?.Version ?? 0);
        }).ToArray();
    }
}
