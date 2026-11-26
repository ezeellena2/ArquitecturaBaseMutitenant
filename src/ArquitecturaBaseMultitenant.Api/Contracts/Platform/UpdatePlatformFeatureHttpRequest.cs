using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed record UpdatePlatformFeatureHttpRequest(bool? Enabled, PlatformFeatureSource? Source,
    DateOnly? ExpiresOn, uint? Version, string? Reason)
{
    public override string ToString() => nameof(UpdatePlatformFeatureHttpRequest);
    public UpdatePlatformFeatureRequest ToModel() => new(Enabled, Source, ExpiresOn, Version, Reason);
}
