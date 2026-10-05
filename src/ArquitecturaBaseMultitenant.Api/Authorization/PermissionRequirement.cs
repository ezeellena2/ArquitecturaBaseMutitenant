using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

internal enum PermissionKind
{
    Organization,
    Company,
    Platform,
}

/// <summary>
/// Un permiso de su clase. En uno de empresa, <paramref name="OrganizationPermission"/> es el permiso de la organización
/// que también alcanza.
/// </summary>
internal sealed record PermissionRequirement(PermissionKind Kind, string Permission,
    string? OrganizationPermission = null) : IAuthorizationRequirement;
