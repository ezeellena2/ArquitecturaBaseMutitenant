using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Combina los permisos efectivos de la persona desde su proyección cacheada (ADR 0052): la de sus roles de
/// organización y, sólo cuando hace falta, la de sus roles por empresa. Los roles de organización valen en toda empresa
/// activa; los de empresa, sólo donde tiene la membresía activa.
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
        {
            var granted = new SortedSet<string>(await OrganizationAsync(userId, cancellationToken), StringComparer.Ordinal);
            foreach (var own in (await ByCompanyAsync(userId, cancellationToken)).Values)
                granted.UnionWith(own.Intersect(Permissions.OrganizationProjectedFromCompany, StringComparer.Ordinal));
            return (IReadOnlyList<string>)[.. granted];
        });

    public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId,
        CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "GetCompanyPermissions",
            () => CompanyAsync(userId, companyId, cancellationToken));

    // Las mismas empresas que «Tus empresas» del inicio: membresía, miembro y empresa activos.
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId,
        CancellationToken cancellationToken) =>
        OperationLog.RunValueAsync(logger, timeProvider, "GetPermissionsByCompany", async () =>
        {
            var organization = await OrganizationAsync(userId, cancellationToken);
            var byCompany = new Dictionary<Guid, IReadOnlyList<string>>();
            foreach (var (companyId, own) in await ByCompanyAsync(userId, cancellationToken))
            {
                var granted = Union(organization, own);
                if (granted.Count > 0) byCompany[companyId] = granted;
            }

            return (IReadOnlyDictionary<Guid, IReadOnlyList<string>>)byCompany;
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

    private Task<IReadOnlyList<string>> OrganizationAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        return ActiveBusinessTenant() is { } tenantId
            ? cache.GetOrganizationPermissionsAsync(tenantId, userId, cancellationToken)
            : Task.FromResult<IReadOnlyList<string>>([]);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ByCompanyAsync(Guid userId,
        CancellationToken cancellationToken) =>
        ActiveBusinessTenant() is { } tenantId
            ? await cache.GetCompanyPermissionsAsync(tenantId, userId, cancellationToken)
            : new Dictionary<Guid, IReadOnlyList<string>>();

    private async Task<IReadOnlyList<string>> CompanyAsync(Guid userId, Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));
        var organization = await OrganizationAsync(userId, cancellationToken);
        if ((await ByCompanyAsync(userId, cancellationToken)).TryGetValue(companyId, out var own))
            return Union(organization, own);
        // Sin membresía en esa empresa sólo valen los roles de organización, y sólo si la empresa está activa.
        return organization.Count > 0 && await companies.IsActiveAsync(companyId, cancellationToken) ? organization : [];
    }

    private Guid? ActiveBusinessTenant() =>
        tenantContext.TenantKind == TenantKind.Business ? tenantContext.TenantId : null;

    private static IReadOnlyList<string> Union(IEnumerable<string> organization, IEnumerable<string> own) =>
        [.. organization.Union(own, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
