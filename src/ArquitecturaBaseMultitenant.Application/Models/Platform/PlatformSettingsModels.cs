using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

public sealed record PlatformSettingsResponse(ConsumerSignupMode ConsumerSignup, BusinessSignupMode BusinessSignup,
    int MaxOwnedOrganizations, Email? SalesEmail, Email? SupportEmail, uint Version);

public sealed record UpdatePlatformSettingsRequest(ConsumerSignupMode? ConsumerSignup, BusinessSignupMode? BusinessSignup,
    int? MaxOwnedOrganizations, Email? SalesEmail, Email? SupportEmail, uint? Version, string? Reason)
{
    public override string ToString() => nameof(UpdatePlatformSettingsRequest);
}

/// <summary>Los correos de contacto que muestran Ingreso y Registro, sin sesión; null mientras no se cargaron.</summary>
public sealed record PlatformContactResponse(Email? SalesEmail, Email? SupportEmail);
