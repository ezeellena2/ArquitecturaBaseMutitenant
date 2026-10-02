using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>Exige un permiso del acceso de plataforma.</summary>
public sealed class HasPlatformPermissionAttribute(string permission)
    : AuthorizeAttribute(PermissionPolicyProvider.PlatformPrefix + permission)
{
    public string Permission { get; } = permission;
}
