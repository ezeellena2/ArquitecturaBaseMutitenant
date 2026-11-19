using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;

/// <summary>
/// La caché de permisos con lo dado, que cuenta cuántas veces se lee cada proyección: con el <c>PermissionService</c> real,
/// así se ve que un pedido lee cada una una sola vez (base de la 12, <c>permisos.md</c>).
/// </summary>
internal sealed class CountingPermissionCache(IReadOnlyList<string> organization,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> companies) : IRolePermissionCache
{
    public int OrganizationReads { get; private set; }

    public int CompanyReads { get; private set; }

    public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken)
    {
        OrganizationReads++;
        return Task.FromResult(organization);
    }

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetCompanyPermissionsAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken)
    {
        CompanyReads++;
        return Task.FromResult(companies);
    }
}
