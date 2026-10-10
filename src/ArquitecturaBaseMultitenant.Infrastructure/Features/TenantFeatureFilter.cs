using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.FeatureManagement;

namespace ArquitecturaBaseMultitenant.Infrastructure.Features;

/// <summary>Filtro scoped del espacio firmado, con aislamiento de alcance y vigencia inmediata.</summary>
[FilterAlias("Tenant")]
internal sealed class TenantFeatureFilter(ITenantContext tenantContext, IFeatureCatalog catalog,
    ITenantFeatureCache cache, TimeProvider timeProvider, ICurrentUser currentUser, IEnumerable<IFeatureAvailabilityPolicy> policies) : IFeatureFilter
{
    private readonly Dictionary<string, IFeatureAvailabilityPolicy> _policies = policies.ToDictionary(policy => policy.Key, StringComparer.Ordinal);

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context) =>
        EvaluateAsync(context, CancellationToken.None);

    public async Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context, CancellationToken cancellationToken)
    {
        var definition = catalog.All.SingleOrDefault(row => row.Key == context.FeatureName);
        if (definition is null) return false;
        if (currentUser.Access is Access.Consumer or Access.Business
            && (tenantContext.TenantKind is not { } scopedKind || !definition.Supports(scopedKind))) return false;
        if (_policies.TryGetValue(context.FeatureName, out var policy))
        {
            if (currentUser.Access is not { } access) return false;
            if (await policy.IsAvailableAsync(access, cancellationToken) is { } available) return available;
        }
        if (currentUser.Access == Access.Platform || tenantContext.TenantId is not { } tenantId
            || tenantContext.TenantKind is not { } kind || !definition.Supports(kind)) return false;
        var grants = await cache.ListAsync(tenantId, cancellationToken);
        var grant = grants.SingleOrDefault(row => row.Key == definition.Key);
        return grant?.IsEnabledAt(timeProvider.GetUtcNow().UtcDateTime) ?? definition.DefaultEnabled;
    }
}
