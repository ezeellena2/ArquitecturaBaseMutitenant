using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Features;

/// <summary>Adapta FeatureManagement para el núcleo sin exponer la librería a Application.</summary>
internal sealed class FeatureService(IFeatureManager manager, IFeatureCatalog catalog,
    ITenantContext tenantContext, TimeProvider timeProvider, ILogger<FeatureService> logger) : IFeatureService
{
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
        return definition is not null && tenantContext.TenantKind is { } kind && definition.Supports(kind)
            && await manager.IsEnabledAsync(key);
    }
}
