using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Nadie da lo que no tiene, en la organización (ADR 0061). Decide sin leer ni escribir, con lo efectivo de quien pide
/// (<see cref="PermissionsByPlace"/>, decisión 260): si cambia los permisos de un rol, si da o quita roles a una persona,
/// si actúa sobre ella y qué puede dar (<c>grantable</c>, decisión 269). La usan <see cref="OrganizationDelegationGuard"/>,
/// los listados de roles y permisos y las acciones del asistente (ADR 0068), así la pantalla y la ruta deciden igual.
/// <list type="bullet">
/// <item>En la organización cuenta lo que mira <c>[HasPermission]</c>: sus roles de organización más lo que una empresa
/// proyecta a la organización (<see cref="Permissions.OrganizationProjectedFromCompany"/>). Los permisos de un rol se
/// comparan siempre ahí: administrar roles es de la organización.</item>
/// <item>En una empresa cuenta lo que mira <c>[HasCompanyPermission]</c>: sus roles de organización más los de esa
/// empresa. Sin empresa (un rol que vale en cualquiera), solo los de organización, que valen en todas.</item>
/// <item>Actuar sobre alguien (desactivarlo, reactivarlo, quitarlo o cambiarle los roles) pide tener todo lo que le dan
/// sus roles, en cada lugar: los de organización en la organización y, en cada empresa, los de organización más los de
/// esa empresa. Sobre uno mismo siempre pasa.</item>
/// </list>
/// </summary>
internal static class OrganizationDelegationPolicy
{
    /// <summary>Lo que tiene en la organización, como lo mira <c>[HasPermission]</c>.</summary>
    public static IReadOnlySet<string> InOrganization(PermissionsByPlace held)
    {
        ArgumentNullException.ThrowIfNull(held);
        var granted = new HashSet<string>(held.Organization, StringComparer.Ordinal);
        foreach (var own in held.Companies.Values)
            granted.UnionWith(own.Where(permission =>
                Permissions.OrganizationProjectedFromCompany.Contains(permission, StringComparer.Ordinal)));
        return granted;
    }

    /// <summary>
    /// Lo que tiene en una empresa, como lo mira <c>[HasCompanyPermission]</c>; sin empresa, lo que tiene en cualquiera.
    /// </summary>
    public static IReadOnlySet<string> InCompany(PermissionsByPlace held, Guid? companyId)
    {
        ArgumentNullException.ThrowIfNull(held);
        var granted = new HashSet<string>(held.Organization, StringComparer.Ordinal);
        if (companyId is { } id && held.Companies.TryGetValue(id, out var own)) granted.UnionWith(own);
        return granted;
    }

    /// <summary>Si puede dar este permiso en un rol (<c>grantable</c> del catálogo): si lo tiene en la organización.</summary>
    public static bool CanGrantPermission(PermissionsByPlace held, string permission) =>
        InOrganization(held).Contains(permission);

    /// <summary>
    /// Crear o editar un rol: cada permiso que entra o sale, con lo que incluye, lo tiene en la organización. El nombre,
    /// la descripción y dónde vale no cuentan.
    /// </summary>
    public static bool CanChangePermissions(PermissionsByPlace held, IEnumerable<string> before, IEnumerable<string> after)
    {
        var changed = new HashSet<string>(Effective(before), StringComparer.Ordinal);
        changed.SymmetricExceptWith(Effective(after));
        return changed.IsSubsetOf(InOrganization(held));
    }

    /// <summary>
    /// Dar o quitar un rol a una persona: todo lo que el rol permite lo tiene donde se da. Sin empresa, en la organización.
    /// </summary>
    public static bool CanGrantRole(PermissionsByPlace held, IEnumerable<string> rolePermissions, Guid? companyId) =>
        Effective(rolePermissions).IsSubsetOf(companyId is null ? InOrganization(held) : InCompany(held, companyId));

    /// <summary>
    /// Si puede dar un rol en un listado (<c>grantable</c>): un rol de organización, en la organización; uno de empresa, en
    /// la suya, o en <paramref name="companyId"/> si vale en cualquiera, o en todas si no hay empresa.
    /// </summary>
    public static bool CanGrantListedRole(PermissionsByPlace held, RoleScope scope, Guid? roleCompanyId,
        IEnumerable<string> rolePermissions, Guid? companyId) =>
        scope == RoleScope.Organization
            ? CanGrantRole(held, rolePermissions, companyId: null)
            : Effective(rolePermissions).IsSubsetOf(InCompany(held, roleCompanyId ?? companyId));

    /// <summary>Dar o quitar estos roles: cada uno, donde se da o se quita.</summary>
    public static bool CanChangeRoles(PermissionsByPlace held, IEnumerable<RoleGrant> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        return changed.All(grant => CanGrantRole(held, grant.Permissions, grant.CompanyId));
    }

    /// <summary>
    /// Actuar sobre una persona: tiene todo lo que le dan sus roles, en la organización y en cada empresa donde tiene
    /// alguno. Sobre uno mismo siempre.
    /// </summary>
    public static bool CanActOn(bool self, PermissionsByPlace held, PermissionsByPlace target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (self) return true;
        if (!target.Organization.IsSubsetOf(InOrganization(held))) return false;
        return target.Companies.All(company => InCompany(target, company.Key)
            .IsSubsetOf(InCompany(held, company.Key)));
    }

    private static HashSet<string> Effective(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return new HashSet<string>(Permissions.Inclusions.Effective(permissions), StringComparer.Ordinal);
    }
}

/// <summary>
/// Lo que dan los roles de una persona en una organización, por lugar y ya efectivo (decisión 260): los de organización y,
/// por cada empresa, los de esa empresa, sin los de organización. Para quien pide, solo lo que hoy vale (miembro,
/// membresía y empresa activos); para la persona sobre la que se actúa, todo lo que tiene asignado.
/// </summary>
internal sealed record PermissionsByPlace(IReadOnlySet<string> Organization,
    IReadOnlyDictionary<Guid, IReadOnlySet<string>> Companies)
{
    public static PermissionsByPlace None { get; } = Of([], new Dictionary<Guid, IEnumerable<string>>());

    /// <summary>Lo efectivo de lo asignado en la organización y en cada empresa; un código fuera del catálogo no cuenta.</summary>
    public static PermissionsByPlace Of(IEnumerable<string> organization,
        IReadOnlyDictionary<Guid, IEnumerable<string>> companies)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(companies);
        return new(Effective(organization), companies.ToDictionary(company => company.Key,
            company => (IReadOnlySet<string>)Effective(company.Value)));
    }

    private static HashSet<string> Effective(IEnumerable<string> permissions) =>
        new(Permissions.Inclusions.Effective(permissions), StringComparer.Ordinal);
}

/// <summary>Un rol que se da o se quita: lo que permite y dónde; sin empresa, en la organización.</summary>
internal sealed record RoleGrant(IReadOnlyCollection<string> Permissions, Guid? CompanyId);
