using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyecta roles del tenant activo sin seguimiento de EF.</summary>
internal sealed class RoleReader(ApplicationDbContext context, ITenantContext tenantContext) : IRoleReader
{
    public async Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var rows = await context.Roles.AsNoTracking()
            .Where(role => role.TenantId == tenantId)
            .OrderBy(role => role.Name).ThenBy(role => role.Id)
            .Select(role => new RoleRow(role.Id, role.Name, role.Description, role.Scope, role.CompanyId,
                role.IsSystemRole, role.Permissions.ToArray(), role.Version, role.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return [.. rows.Select(row => row with { Permissions = row.Permissions.Order(StringComparer.Ordinal).ToArray() })];
    }

    public async Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var row = await context.Roles.AsNoTracking()
            .Where(role => role.TenantId == tenantId && role.Id == roleId)
            .Select(role => new RoleRow(role.Id, role.Name, role.Description, role.Scope, role.CompanyId,
                role.IsSystemRole, role.Permissions.ToArray(), role.Version, role.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : row with
        {
            Permissions = row.Permissions.Order(StringComparer.Ordinal).ToArray(),
        };
    }

    public Task<bool> ExistsByNameAsync(string name, Guid? excludedRoleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var tenantId = tenantContext.RequiredTenantId;
        var normalized = Role.NormalizeName(name);
        return context.Roles.AsNoTracking().AnyAsync(role => role.TenantId == tenantId
            && role.NormalizedName == normalized
            && (excludedRoleId == null || role.Id != excludedRoleId), cancellationToken);
    }
}
