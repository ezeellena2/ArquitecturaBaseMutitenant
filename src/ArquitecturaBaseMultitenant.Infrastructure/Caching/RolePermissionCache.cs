using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Los permisos que dan los roles de una persona en la caché compartida (ADR 0052), 60 s: los de organización y, en otra
/// entrada, los de cada empresa. Cada carga lee con RLS en un scope propio de esa organización y en consultas agrupadas,
/// sin importar cuántas empresas haya. Una clave privada se usa sólo desde un pedido de esa misma organización.
/// </summary>
internal sealed class RolePermissionCache(SharedCache cache, ITenantContext tenantContext) : IRolePermissionCache
{
    private static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken)
    {
        RequireActiveOrganization(tenantId);
        return await cache.GetOrLoadAsync(
            new CacheEntry(Key(tenantId, userId, "organization"), OrganizationTags(tenantId, userId), Expiration),
            (TenantId: tenantId, UserId: userId),
            static async (services, state, ct) =>
            {
                services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(state.TenantId, TenantKind.Business);
                var roles = services.GetRequiredService<IRoleReader>();
                var granted = await roles.ListPermissionsByRoleAsync(
                    await roles.ListOrganizationRoleIdsAsync(state.UserId, ct), ct);
                return Union(granted.Values);
            },
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetCompanyPermissionsAsync(Guid tenantId,
        Guid userId, CancellationToken cancellationToken)
    {
        RequireActiveOrganization(tenantId);
        var byCompany = await cache.GetOrLoadAsync(
            new CacheEntry(Key(tenantId, userId, "companies"),
                [.. OrganizationTags(tenantId, userId), CacheTags.Companies(tenantId)], Expiration),
            (TenantId: tenantId, UserId: userId),
            static async (services, state, ct) =>
            {
                services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(state.TenantId, TenantKind.Business);
                var roles = services.GetRequiredService<IRoleReader>();
                var assigned = await roles.ListCompanyRoleIdsAsync(state.UserId, ct);
                var granted = await roles.ListPermissionsByRoleAsync(
                    [.. assigned.Values.SelectMany(roleIds => roleIds).Distinct()], ct);
                return assigned.ToDictionary(company => company.Key, company => Union(company.Value
                    .Select(roleId => granted.GetValueOrDefault(roleId) ?? [])));
            },
            cancellationToken);
        return byCompany.ToDictionary(company => company.Key, company => (IReadOnlyList<string>)company.Value);
    }

    private void RequireActiveOrganization(Guid tenantId)
    {
        if (tenantContext.TenantId != tenantId || tenantContext.TenantKind != TenantKind.Business)
            throw new InvalidOperationException("Cached permissions are read only for the active business organization.");
    }

    private static string[] OrganizationTags(Guid tenantId, Guid userId) =>
    [
        CacheTags.Organization(tenantId), CacheTags.Account(userId), CacheTags.Members(tenantId),
        CacheTags.Roles(tenantId), CacheTags.Assignments(tenantId),
    ];

    private static string Key(Guid tenantId, Guid userId, string part) => CacheKeys.Tenant(tenantId,
        "permissions:" + userId.ToString("N", CultureInfo.InvariantCulture) + ":" + part);

    private static string[] Union(IEnumerable<IEnumerable<string>> permissions) =>
        [.. permissions.SelectMany(granted => granted).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
