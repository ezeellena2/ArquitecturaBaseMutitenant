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
        // La pestaña muestra solo la fila de Plataforma (Plan, Manual o Trial), nunca una de otro origen (ADR 0069). El
        // reader trae cada clave de la más reciente a la más vieja: si hubiera varias de Plataforma, vale la más reciente.
        var grants = await features.ListAsync(tenantId, ct);
        return catalog.All.Where(definition => definition.Supports(tenant.Kind)).Select(definition =>
        {
            var grant = grants.FirstOrDefault(row => row.Key == definition.Key && row.Source.IsPlatformManaged());
            return PlatformFeatureResponse.From(definition.Key, grant?.Enabled ?? definition.DefaultEnabled,
                grant?.Source ?? TenantFeatureSource.Plan, grant?.ExpiresOn, grant?.Version ?? 0);
        }).ToArray();
    }
}
