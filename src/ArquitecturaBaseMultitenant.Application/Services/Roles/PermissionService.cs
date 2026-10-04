using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>Combina permisos de las asignaciones activas; solo cachea el catálogo inmutable de cada rol.</summary>
internal sealed class PermissionService(IRoleReader roles, IRolePermissionCache cache, ITenantContext tenantContext,
    ICompanyMembershipReader memberships) : IPermissionService
{
    public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId,
        CancellationToken cancellationToken) => GetAsync(userId, companyId: null, cancellationToken);

    public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));
        return GetAsync(userId, companyId, cancellationToken);
    }

    // Las mismas empresas que «Tus empresas» del inicio: membresía, miembro y empresa activos.
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        var byCompany = new Dictionary<Guid, IReadOnlyList<string>>();
        if (tenantContext.TenantId is null || tenantContext.TenantKind != TenantKind.Business) return byCompany;
        foreach (var company in await memberships.ListActiveForUserAsync(userId, cancellationToken))
        {
            var granted = await GetAsync(userId, company.CompanyId, cancellationToken);
            if (granted.Count > 0) byCompany[company.CompanyId] = granted;
        }

        return byCompany;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
        (await GetOrganizationPermissionsAsync(userId, cancellationToken)).Contains(permission, StringComparer.Ordinal);

    public async Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission,
        CancellationToken cancellationToken) =>
        (await GetCompanyPermissionsAsync(userId, companyId, cancellationToken)).Contains(permission, StringComparer.Ordinal);

    public Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("Role permissions require an active organization.");
        return cache.InvalidateRoleAsync(tenantId, roleId, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetAsync(Guid userId, Guid? companyId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        if (tenantContext.TenantId is not { } tenantId || tenantContext.TenantKind != TenantKind.Business)
            return [];

        var roleIds = await roles.ListRoleIdsForUserAsync(userId, companyId, cancellationToken);
        var permissions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var roleId in roleIds)
        {
            permissions.UnionWith(await cache.GetRolePermissionsAsync(tenantId, roleId, cancellationToken));
        }

        return [.. permissions];
    }
}
