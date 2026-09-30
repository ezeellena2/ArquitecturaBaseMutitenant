using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class AccountDeletionTenantReader(ApplicationDbContext context) : IAccountDeletionTenantReader
{
    public async Task<IReadOnlyList<AccountDeletionTenant>> ListAsync(Guid userId, CancellationToken ct) =>
        await (from access in context.UserTenantAccesses.AsNoTracking()
            where access.UserId == userId
            join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
            orderby tenant.Kind, tenant.Id
            select new AccountDeletionTenant(tenant.Id, tenant.Kind)).ToArrayAsync(ct);
}
