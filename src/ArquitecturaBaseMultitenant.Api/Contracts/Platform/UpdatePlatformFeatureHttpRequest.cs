using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed record UpdatePlatformFeatureHttpRequest(bool? Enabled, TenantFeatureSource? Source,
    DateTime? ExpiresAtUtc, uint? Version, string? Reason)
{
    public override string ToString() => nameof(UpdatePlatformFeatureHttpRequest);
    public UpdatePlatformFeatureRequest ToModel() => new(Enabled, Source, ExpiresAtUtc, Version, Reason);
}
