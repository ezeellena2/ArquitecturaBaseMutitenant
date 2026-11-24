using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Combina los permisos efectivos de la persona desde su proyección cacheada (ADR 0052): la de sus roles de
/// organización y, sólo cuando hace falta, la de sus roles por empresa. Los roles de organización valen en toda empresa
/// activa; los de empresa, sólo donde tiene la membresía activa. Cada proyección es lo efectivo (decisión 260): los
/// permisos del catálogo de esta versión más lo que incluyen, en cadena. Se calcula acá, al leer: la caché guarda lo
/// asignado más lo incluido sin descartar lo que esta versión no conoce, y la puede haber llenado la versión con la que
/// convive (ADR 0060).
/// </summary>
internal sealed class PermissionService(IRolePermissionCache cache, ITenantContext tenantContext,
    ICompanyReader companies, TimeProvider timeProvider, ILogger<PermissionService> logger) : IPermissionService
{
    public Task<IReadOnlyList<string>> GetAssignedOrganizationPermissionsAsync(Guid userId,
        CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "GetAssignedOrganizationPermissions",
            () => OrganizationAsync(userId, cancellationToken));

    public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId,
        CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "GetOrganizationPermissions", async () =>
            WithProjections(await OrganizationAsync(userId, cancellationToken), await ByCompanyAsync(userId, cancellationToken)));

    public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId,
        CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "GetCompanyPermissions",
            () => CompanyAsync(userId, companyId, cancellationToken));

    // Las mismas empresas que «Tus empresas» del inicio: membresía, miembro y empresa activos.
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId,
        CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "GetPermissionsByCompany", async () =>
            (IReadOnlyDictionary<Guid, IReadOnlyList<string>>)PerCompany(await OrganizationAsync(userId, cancellationToken),
                await ByCompanyAsync(userId, cancellationToken)));

    // Una lectura por proyección (base de la 12): las dos vistas salen de lo mismo leído.
    public Task<BusinessPermissions> GetBusinessPermissionsAsync(Guid userId, CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "GetBusinessPermissions", async () =>
        {
            var organization = await OrganizationAsync(userId, cancellationToken);
            var byCompany = await ByCompanyAsync(userId, cancellationToken);
            return new BusinessPermissions(WithProjections(organization, byCompany), PerCompany(organization, byCompany),
                organization) { TenantId = ActiveBusinessTenant() };
        });

    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "HasPermission", async () =>
            (await OrganizationAsync(userId, cancellationToken)).Contains(permission, StringComparer.Ordinal)
            // Sólo un permiso que se proyecta desde una empresa consulta las empresas.
            || Permissions.OrganizationProjectedFromCompany.Contains(permission, StringComparer.Ordinal)
                && (await ByCompanyAsync(userId, cancellationToken)).Values
                    .Any(own => own.Contains(permission, StringComparer.Ordinal)));

    public Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission,
        CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "HasCompanyPermission", async () =>
            (await CompanyAsync(userId, companyId, cancellationToken)).Contains(permission, StringComparer.Ordinal));

    // Lo mismo que CompanyFromAsync sin releer: ByCompany ya trae la unión de cada empresa donde es miembro con alguno, y
    // una empresa sin ninguno ahí tampoco tiene roles de organización que sumar.
    public Task<bool> HasCompanyPermissionAsync(BusinessPermissions granted, Guid companyId, string permission,
        CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "HasCompanyPermissionFromGranted", async () =>
        {
            ArgumentNullException.ThrowIfNull(granted);
            if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));
            // Lo leído en una organización nunca decide en otra: sería un bug de quien lo guardó.
            if (granted.TenantId != ActiveBusinessTenant())
                throw new InvalidOperationException("The permissions were read in another organization.");
            return granted.ByCompany.TryGetValue(companyId, out var own)
                ? own.Contains(permission, StringComparer.Ordinal)
                : granted.Assigned.Contains(permission, StringComparer.Ordinal)
                    && await companies.IsActiveAsync(companyId, cancellationToken);
        });

    // Una lectura por requisito (base de la 12): la proyección de la organización se lee una vez y sirve a la alternativa y
    // a la empresa; la de las empresas, solo si hace falta y también una vez.
    public Task<bool> HasCompanyRequirementAsync(Guid userId, Guid companyId, string permission,
        string? organizationPermission, CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "HasCompanyRequirement", async () =>
        {
            if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));
            var organization = await OrganizationAsync(userId, cancellationToken);
            if (organizationPermission is not null && organization.Contains(organizationPermission, StringComparer.Ordinal))
                return true;
            var byCompany = await ByCompanyAsync(userId, cancellationToken);
            if (organizationPermission is not null
                && Permissions.OrganizationProjectedFromCompany.Contains(organizationPermission, StringComparer.Ordinal)
                && byCompany.Values.Any(own => own.Contains(organizationPermission, StringComparer.Ordinal)))
                return true;
            return (await CompanyFromAsync(companyId, organization, byCompany, cancellationToken))
                .Contains(permission, StringComparer.Ordinal);
        });

    private async Task<IReadOnlyList<string>> OrganizationAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        return ActiveBusinessTenant() is { } tenantId
            ? Effective(await cache.GetOrganizationPermissionsAsync(tenantId, userId, cancellationToken))
            : [];
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ByCompanyAsync(Guid userId,
        CancellationToken cancellationToken) =>
        ActiveBusinessTenant() is { } tenantId
            ? (await cache.GetCompanyPermissionsAsync(tenantId, userId, cancellationToken))
                .ToDictionary(company => company.Key, company => Effective(company.Value))
            : new Dictionary<Guid, IReadOnlyList<string>>();

    private static IReadOnlyList<string> Effective(IEnumerable<string> granted) =>
        [.. Permissions.Inclusions.Effective(granted).Order(StringComparer.Ordinal)];

    private async Task<IReadOnlyList<string>> CompanyAsync(Guid userId, Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));
        return await CompanyFromAsync(companyId, await OrganizationAsync(userId, cancellationToken),
            await ByCompanyAsync(userId, cancellationToken), cancellationToken);
    }

    private async Task<IReadOnlyList<string>> CompanyFromAsync(Guid companyId, IReadOnlyList<string> organization,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> byCompany, CancellationToken cancellationToken)
    {
        if (byCompany.TryGetValue(companyId, out var own)) return Union(organization, own);
        // Sin membresía en esa empresa sólo valen los roles de organización, y sólo si la empresa está activa.
        return organization.Count > 0 && await companies.IsActiveAsync(companyId, cancellationToken) ? organization : [];
    }

    // Los de la organización más los que se proyectan desde alguna empresa.
    private static IReadOnlyList<string> WithProjections(IReadOnlyList<string> organization,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> byCompany)
    {
        var granted = new SortedSet<string>(organization, StringComparer.Ordinal);
        foreach (var own in byCompany.Values)
            granted.UnionWith(own.Intersect(Permissions.OrganizationProjectedFromCompany, StringComparer.Ordinal));
        return [.. granted];
    }

    // Cada empresa con alguno, con los de la organización incluidos.
    private static Dictionary<Guid, IReadOnlyList<string>> PerCompany(IReadOnlyList<string> organization,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> byCompany)
    {
        var granted = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var (companyId, own) in byCompany)
        {
            var union = Union(organization, own);
            if (union.Count > 0) granted[companyId] = union;
        }

        return granted;
    }

    private Guid? ActiveBusinessTenant() =>
        tenantContext.TenantKind == TenantKind.Business ? tenantContext.TenantId : null;

    private static IReadOnlyList<string> Union(IEnumerable<string> organization, IEnumerable<string> own) =>
        [.. organization.Union(own, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
