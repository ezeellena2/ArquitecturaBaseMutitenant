using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

internal enum PermissionKind
{
    Organization,
    Company,
    Platform,
}

internal sealed record PermissionRequirement(PermissionKind Kind, string Permission) : IAuthorizationRequirement;
