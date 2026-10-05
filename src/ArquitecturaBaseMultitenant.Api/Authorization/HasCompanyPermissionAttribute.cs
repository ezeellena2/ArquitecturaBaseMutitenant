using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>
/// Exige un permiso de empresa y toma su alcance del companyId de la ruta. Con <paramref name="organizationPermission"/>,
/// también alcanza ese permiso de la organización: así la ficha de una empresa la abre quien ve todas las empresas y
/// quien tiene roles en esa.
/// </summary>
public sealed class HasCompanyPermissionAttribute(string permission, string? organizationPermission = null)
    : AuthorizeAttribute(PermissionPolicyProvider.CompanyPolicy(permission, organizationPermission))
{
    public string Permission { get; } = permission;

    public string? OrganizationPermission { get; } = organizationPermission;
}
