using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Solo proyecta el índice global de accesos; nunca lee Members fuera de su RLS.</summary>
internal sealed class UserTenantAccessReader(ApplicationDbContext context) : IUserTenantAccessReader
{
    public async Task<IReadOnlyList<UserTenantAccessRow>> ListForUserAsync(Guid userId,
        CancellationToken cancellationToken) =>
        await (from access in context.UserTenantAccesses.AsNoTracking()
               join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
               where access.UserId == userId
               orderby access.JoinedAtUtc, tenant.Name, tenant.Id
               select new UserTenantAccessRow(tenant.Id, tenant.Kind, tenant.Name, tenant.Slug,
                   tenant.Status, access.Status, access.JoinedAtUtc))
            .ToArrayAsync(cancellationToken);
}
