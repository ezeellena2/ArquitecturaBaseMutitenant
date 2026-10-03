using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using Microsoft.FeatureManagement;

namespace ArquitecturaBaseMultitenant.Infrastructure.Features;

/// <summary>Filtro scoped del espacio firmado, con aislamiento de alcance y vigencia inmediata.</summary>
[FilterAlias("Tenant")]
internal sealed class TenantFeatureFilter(ITenantContext tenantContext, IFeatureCatalog catalog,
    ITenantFeatureCache cache, TimeProvider timeProvider) : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context) =>
        EvaluateAsync(context, CancellationToken.None);

    public async Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context, CancellationToken cancellationToken)
    {
        var definition = catalog.All.SingleOrDefault(row => row.Key == context.FeatureName);
        if (definition is null || tenantContext.TenantId is not { } tenantId
            || tenantContext.TenantKind is not { } kind || !definition.Supports(kind)) return false;
        var grants = await cache.ListAsync(tenantId, cancellationToken);
        var grant = grants.SingleOrDefault(row => row.Key == definition.Key);
        return grant?.IsEnabledAt(timeProvider.GetUtcNow().UtcDateTime) ?? definition.DefaultEnabled;
    }
}
