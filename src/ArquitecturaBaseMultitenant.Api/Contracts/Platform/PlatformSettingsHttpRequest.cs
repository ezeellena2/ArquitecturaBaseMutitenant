using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed record UpdatePlatformSettingsHttpRequest(ConsumerSignupMode? ConsumerSignup, BusinessSignupMode? BusinessSignup,
    int? MaxOwnedOrganizations, uint? Version, string? Reason)
{
    public override string ToString() => nameof(UpdatePlatformSettingsHttpRequest);

    public UpdatePlatformSettingsRequest ToModel() => new(ConsumerSignup, BusinessSignup, MaxOwnedOrganizations, Version, Reason);
}
