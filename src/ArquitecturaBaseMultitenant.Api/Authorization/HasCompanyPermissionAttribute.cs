using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>Exige un permiso de empresa y toma su alcance del companyId de la ruta.</summary>
public sealed class HasCompanyPermissionAttribute(string permission)
    : AuthorizeAttribute(PermissionPolicyProvider.CompanyPrefix + permission)
{
    public string Permission { get; } = permission;
}
