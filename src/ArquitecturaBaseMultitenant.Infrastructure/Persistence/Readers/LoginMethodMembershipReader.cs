using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Comprueba si la cuenta conserva una membresía y una organización activas antes de usar un método administrado. Lee el índice global de accesos, sin saltar RLS de Members.</summary>
internal sealed class LoginMethodMembershipReader(ApplicationDbContext context) : ILoginMethodMembershipReader
{
    public Task<bool> IsActiveAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) =>
        (from access in context.UserTenantAccesses.AsNoTracking()
         join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
         where access.UserId == userId && access.TenantId == tenantId
             && access.Status == MemberStatus.Active && tenant.Status == TenantStatus.Active
         select access.UserId).AnyAsync(cancellationToken);
}
