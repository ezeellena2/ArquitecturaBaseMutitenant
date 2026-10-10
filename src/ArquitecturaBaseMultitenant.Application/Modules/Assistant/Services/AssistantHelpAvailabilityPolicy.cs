using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantHelpAvailabilityPolicy(IFeatureService features, IFeatureCatalog catalog,
    IEnumerable<ILoginCodeChannel> channels)
{
    internal Task<bool> IsAvailableAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();
        // A defined feature remains authoritative even when a channel happens to share its key.
        return catalog.All.Any(feature => string.Equals(feature.Key, key, StringComparison.Ordinal))
            ? features.IsEnabledAsync(key, ct)
            : Task.FromResult(channels.Any(channel => string.Equals(channel.Key, key, StringComparison.Ordinal)
                && channel.IsAvailable));
    }
}
