using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authentication;
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

    public async Task<IReadOnlyList<Guid>> ListOwnedBusinessTenantIdsAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        var tenantIds = await (from access in context.UserTenantAccesses.AsNoTracking()
                               join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
                               where access.UserId == userId && access.RoleName == SystemRoles.TenantAdmin
                                   && access.Status == MemberStatus.Active && tenant.Kind == TenantKind.Business
                                   && tenant.Status != TenantStatus.Closed
                               select tenant.Id)
            .Distinct().Order().ToArrayAsync(ct);
        return tenantIds.Length > 0 && await users.ListActiveAsync([userId], ct) is { Count: > 0 }
            ? tenantIds : [];
    }

    public async Task<IReadOnlyList<Guid>> ListOwnedOpenTenantIdsAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        return await (from access in context.UserTenantAccesses.AsNoTracking()
                      join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
                      where access.UserId == userId && access.RoleName == SystemRoles.TenantAdmin
                          && access.Status == MemberStatus.Active && tenant.Kind == TenantKind.Business
                          && tenant.Status != TenantStatus.Closed
                      select tenant.Id)
            .Distinct().Order().ToArrayAsync(ct);
    }

    public async Task<int> CountOtherActiveOwnersAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        var userIds = await context.UserTenantAccesses.AsNoTracking()
            .Where(access => access.TenantId == tenantId && access.UserId != userId
                && access.RoleName == SystemRoles.TenantAdmin && access.Status == MemberStatus.Active)
            .Select(access => access.UserId).Distinct().ToArrayAsync(ct);
        return (await users.ListActiveAsync(userIds, ct)).Count;
    }

    public async Task<IReadOnlyList<ActiveTenantAdminEmail>> ListActiveVerifiedEmailRecipientsAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        var candidates = await (from access in context.UserTenantAccesses.AsNoTracking()
            join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
            join method in context.LoginMethods.AsNoTracking() on access.UserId equals method.UserId
            where access.TenantId == tenantId && access.RoleName == SystemRoles.TenantAdmin
                && access.Status == MemberStatus.Active && tenant.Kind == TenantKind.Business
                && tenant.Status == TenantStatus.Active && method.Type == LoginMethodType.Email
                && method.VerifiedAtUtc != null && (method.ManagedByTenantId == null ||
                    (from managedAccess in context.UserTenantAccesses.AsNoTracking()
                     join manager in context.Tenants.AsNoTracking() on managedAccess.TenantId equals manager.Id
                     where managedAccess.UserId == access.UserId && managedAccess.TenantId == method.ManagedByTenantId
                         && managedAccess.Status == MemberStatus.Active && manager.Status == TenantStatus.Active
                     select managedAccess.UserId).Any())
            select new ActiveTenantAdminEmail(access.UserId, method.Value)).Distinct().ToArrayAsync(ct);
        var active = await users.ListActiveAsync(candidates.Select(value => value.UserId).Distinct().ToArray(), ct);
        return candidates.Where(value => active.Contains(value.UserId)).OrderBy(value => value.UserId).ThenBy(value => value.Email, StringComparer.Ordinal).ToArray();
    }
}
