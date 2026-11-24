using ArquitecturaBaseMultitenant.Application.Services.Roles;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>
/// Nadie da lo que no tiene, en una empresa (ADR 0061 §3). Decide sin leer ni escribir, con lo efectivo de quien pide
/// (<see cref="PermissionsByPlace"/>), contando en la empresa lo que mira <c>[HasCompanyPermission]</c>: sus roles de
/// organización más los de esa empresa. La usan <see cref="CompanyDelegationGuard"/>, el listado de roles de la empresa
/// (<c>grantable</c>, decisión 269) y las acciones del asistente (ADR 0068).
/// <list type="bullet">
/// <item>Sumar a alguien con roles, o cambiarle los roles: cada rol que entra o sale permite solo lo que quien pide tiene
/// en la empresa.</item>
/// <item>Desactivarlo, reactivarlo o cambiarle los roles: quien pide tiene todos sus permisos ahí, también los que le dan
/// sus roles de organización, que valen en la empresa. Sobre uno mismo siempre pasa.</item>
/// </list>
/// </summary>
internal static class CompanyDelegationPolicy
{
    /// <summary>Si puede dar este rol en la empresa: todo lo que permite lo tiene ahí.</summary>
    public static bool CanGrantRole(PermissionsByPlace held, Guid companyId, IEnumerable<string> rolePermissions) =>
        OrganizationDelegationPolicy.CanGrantRole(held, rolePermissions, companyId);

    /// <summary>Dar o quitar estos roles en la empresa: cada uno permite solo lo que tiene ahí.</summary>
    public static bool CanChangeRoles(PermissionsByPlace held, Guid companyId,
        IEnumerable<IEnumerable<string>> changedRoles)
    {
        ArgumentNullException.ThrowIfNull(changedRoles);
        return changedRoles.All(role => CanGrantRole(held, companyId, role));
    }

    /// <summary>Actuar sobre alguien en la empresa: tiene ahí todos sus permisos. Sobre uno mismo siempre.</summary>
    public static bool CanActOn(bool self, PermissionsByPlace held, Guid companyId, PermissionsByPlace target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return self || OrganizationDelegationPolicy.InCompany(target, companyId)
            .IsSubsetOf(OrganizationDelegationPolicy.InCompany(held, companyId));
    }
}
