using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Features;

/// <summary>Adapta FeatureManagement para el núcleo sin exponer la librería a Application.</summary>
internal sealed class FeatureService(IFeatureManager manager, IFeatureCatalog catalog,
    ITenantContext tenantContext, IConfiguration configuration, TimeProvider timeProvider,
    ILogger<FeatureService> logger, ICurrentUser currentUser, IEnumerable<IFeatureAvailabilityPolicy> policies) : IFeatureService
{
    private readonly Dictionary<string, IFeatureAvailabilityPolicy> _policies = policies.ToDictionary(policy => policy.Key, StringComparer.Ordinal);

    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken)
    {
        var result = await OperationLog.RunAsync<bool>(logger, timeProvider, "IsFeatureEnabled", async () =>
            await IsEnabledCoreAsync(key, cancellationToken));
        return result.Value;
    }

    public async Task<IReadOnlyList<string>> ListEnabledAsync(CancellationToken cancellationToken)
    {
        var result = await OperationLog.RunAsync<IReadOnlyList<string>>(logger, timeProvider, "ListEnabledFeatures", async () =>
        {
            var enabled = new List<string>();
            foreach (var definition in catalog.All)
                if (await IsEnabledCoreAsync(definition.Key, cancellationToken)) enabled.Add(definition.Key);
            return enabled;
        });
        return result.Value;
    }

    private async Task<bool> IsEnabledCoreAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var definition = catalog.All.SingleOrDefault(row => row.Key == key);
        var emergencyDisabled = bool.TryParse(configuration[$"FeatureManagement:{key}"], out var enabled)
            && !enabled;
        if (definition is null || emergencyDisabled) return false;
        if (currentUser.Access is Access.Consumer or Access.Business
            && (tenantContext.TenantKind is not { } scopedKind || !definition.Supports(scopedKind))) return false;
        if (_policies.TryGetValue(key, out var policy))
        {
            if (currentUser.Access is not { } access) return false;
            if (await policy.IsAvailableAsync(access, cancellationToken) is { } available) return available;
        }
        if (currentUser.Access == Access.Platform) return false;
        return tenantContext.TenantKind is { } kind && definition.Supports(kind)
            && await manager.IsEnabledAsync(key);
    }
}
