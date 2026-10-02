using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>Exige un permiso de organización, nunca un nombre de rol.</summary>
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute(PermissionPolicyProvider.OrganizationPrefix + permission)
{
    public string Permission { get; } = permission;
}
