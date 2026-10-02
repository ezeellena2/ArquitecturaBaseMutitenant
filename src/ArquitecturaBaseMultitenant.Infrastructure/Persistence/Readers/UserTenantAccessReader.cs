using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Solo proyecta el índice global de accesos; nunca lee Members fuera de su RLS.</summary>
internal sealed class UserTenantAccessReader(ApplicationDbContext context, IUserStatusReader users)
    : IUserTenantAccessReader, ITenantAdminReader
{
    public async Task<IReadOnlyList<UserTenantAccessRow>> ListForUserAsync(Guid userId,
        CancellationToken cancellationToken) =>
        await (from access in context.UserTenantAccesses.AsNoTracking()
               join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
               where access.UserId == userId
               orderby access.JoinedAtUtc, tenant.Name, tenant.Id
               select new UserTenantAccessRow(tenant.Id, tenant.Kind, tenant.Name, tenant.Slug,
                   tenant.Status, access.Status, access.JoinedAtUtc, access.RoleName))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListActiveTenantIdsForOwnerAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        var tenantIds = await (from access in context.UserTenantAccesses.AsNoTracking()
                               join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
                               where access.UserId == userId && access.RoleName == SystemRoles.TenantAdmin
                                   && access.Status == MemberStatus.Active && tenant.Kind == TenantKind.Business
                                   && tenant.Status == TenantStatus.Active
                               select tenant.Id)
            .Distinct().Order().ToArrayAsync(ct);
        return tenantIds.Length > 0 && await users.ListActiveAsync([userId], ct) is { Count: > 0 }
            ? tenantIds : [];
    }

    public async Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        var userIds = await (from access in context.UserTenantAccesses.AsNoTracking()
                             join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
                             where access.TenantId == tenantId && access.RoleName == SystemRoles.TenantAdmin
                                 && access.Status == MemberStatus.Active && tenant.Kind == TenantKind.Business
                                 && tenant.Status == TenantStatus.Active
                             select access.UserId)
            .Distinct().ToArrayAsync(ct);
        return (await users.ListActiveAsync(userIds, ct)).Count;
    }
}
