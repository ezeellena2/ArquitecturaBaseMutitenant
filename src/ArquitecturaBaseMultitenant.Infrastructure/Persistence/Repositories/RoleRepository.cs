using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara la escritura de roles privados; el guardado ocurre en la UoW del caso de uso.</summary>
internal sealed class RoleRepository(ApplicationDbContext context, ITenantContext tenantContext) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return context.Roles.SingleOrDefaultAsync(role => role.Id == roleId, cancellationToken);
    }

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        var normalizedName = Role.NormalizeName(name);
        return context.Roles.SingleOrDefaultAsync(role => role.NormalizedName == normalizedName, cancellationToken);
    }

    public void Add(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Roles.Add(role);
    }

    public void Remove(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Roles.Remove(role);
    }
}
