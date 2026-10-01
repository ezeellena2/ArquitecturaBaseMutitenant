using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyecta sus espacios y todas las organizaciones para limpiar invitaciones aún sin identidad vinculada, sin leer datos privados de otra organización.</summary>
internal sealed class AccountDeletionTenantReader(ApplicationDbContext context) : IAccountDeletionTenantReader
{
    public async Task<IReadOnlyList<AccountDeletionTenant>> ListAsync(Guid userId, CancellationToken ct)
    {
        var tenantIds = await context.UserTenantAccesses.AsNoTracking()
            .Where(access => access.UserId == userId)
            .Select(access => access.TenantId)
            .Union(context.Tenants.AsNoTracking().Where(tenant => tenant.Kind == Domain.Tenancy.TenantKind.Business)
                .Select(tenant => tenant.Id))
            .Distinct()
            .ToArrayAsync(ct);
        return await context.Tenants.AsNoTracking().Where(tenant => tenantIds.Contains(tenant.Id))
            .OrderBy(tenant => tenant.Kind).ThenBy(tenant => tenant.Id)
            .Select(tenant => new AccountDeletionTenant(tenant.Id, tenant.Kind)).ToArrayAsync(ct);
    }
}
