using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;
internal sealed class LoginLinkRepository(ApplicationDbContext context) : ILoginLinkRepository
{
    public Task<LoginLinkOwnerSnapshot?> FindOwnerAsync(Guid userId, Guid methodId, CancellationToken ct) =>
        (from method in context.LoginMethods.AsNoTracking()
         join user in context.Users.AsNoTracking() on method.UserId equals user.Id
         where method.Id == methodId && method.UserId == userId
         select new LoginLinkOwnerSnapshot(
             new UserAccountRow(user.Id, user.DisplayName, user.Culture, user.TimeZoneId, user.Status, user.IsPlatformOperator,
                 null, user.LastBusinessTenantId, user.Version, user.DeletionScheduledForUtc, user.OperatorInvitationExpiresAtUtc, user.ProfileVersion),
             new LoginMethodRow(method.Id, method.Type, method.Value, method.IsPrimary, method.VerifiedAtUtc,
                 method.ManagedByTenantId, method.ContactEmail, method.InvitationOriginTenantId),
             user.LockoutEnabled, user.LockoutEnd == null ? null : user.LockoutEnd.Value.UtcDateTime)).SingleOrDefaultAsync(ct);
    public Task<LoginLinkSnapshot?> FindAsync(string hash, CancellationToken ct) =>
        (from link in context.Set<LoginLink>().AsNoTracking()
         join method in context.LoginMethods.AsNoTracking() on new { Id = link.MethodId, link.UserId } equals new { method.Id, method.UserId }
         join user in context.Users.AsNoTracking() on link.UserId equals user.Id
         where link.TokenHash == hash
         select new LoginLinkSnapshot(link.UserId, link.MethodId, link.ExpiresAtUtc, link.ConsumedAtUtc, link.InvalidatedAtUtc,
             new UserAccountRow(user.Id, user.DisplayName, user.Culture, user.TimeZoneId, user.Status, user.IsPlatformOperator,
                 null, user.LastBusinessTenantId, user.Version, user.DeletionScheduledForUtc, user.OperatorInvitationExpiresAtUtc, user.ProfileVersion),
             new LoginMethodRow(method.Id, method.Type, method.Value, method.IsPrimary, method.VerifiedAtUtc,
                 method.ManagedByTenantId, method.ContactEmail, method.InvitationOriginTenantId),
             user.LockoutEnabled, user.LockoutEnd == null ? null : user.LockoutEnd.Value.UtcDateTime)).SingleOrDefaultAsync(ct);
    public async Task<LoginLink?> GetAsync(string hash, CancellationToken ct)
    {
        context.RequireTransaction();
        var link = await context.Set<LoginLink>().SingleOrDefaultAsync(link => link.TokenHash == hash, ct);
        if (link is not null && context.Entry(link).State == EntityState.Unchanged) await context.Entry(link).ReloadAsync(ct);
        return link;
    }
    public async Task<IReadOnlyList<LoginLink>> ListPendingAsync(Guid userId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<LoginLink>().Where(link => link.UserId == userId && link.ConsumedAtUtc == null
            && link.InvalidatedAtUtc == null).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<DateTime>> ListIssueTimesSinceAsync(Guid userId, DateTime sinceUtc, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<LoginLink>().Where(link => link.UserId == userId && link.CreatedAtUtc > sinceUtc)
            .OrderBy(link => link.CreatedAtUtc).Select(link => link.CreatedAtUtc).ToArrayAsync(ct);
    }
    public void Add(LoginLink link) { context.RequireTransaction(); context.Set<LoginLink>().Add(link); }
}
