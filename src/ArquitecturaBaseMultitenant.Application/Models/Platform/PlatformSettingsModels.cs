using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

public sealed record PlatformSettingsResponse(ConsumerSignupMode ConsumerSignup, BusinessSignupMode BusinessSignup,
    int MaxOwnedOrganizations, uint Version);

public sealed record UpdatePlatformSettingsRequest(ConsumerSignupMode? ConsumerSignup, BusinessSignupMode? BusinessSignup,
    int? MaxOwnedOrganizations, uint? Version, string? Reason)
{
    public override string ToString() => nameof(UpdatePlatformSettingsRequest);
}
