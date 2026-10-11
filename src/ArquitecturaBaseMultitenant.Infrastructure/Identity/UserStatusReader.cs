using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>Lee sólo el estado de una identidad global.</summary>
internal sealed class UserStatusReader(ApplicationDbContext context) : IUserStatusReader
{
    public Task<UserStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (UserStatus?)user.Status)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> ListActiveAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0) return [];
        return await context.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.Status == UserStatus.Active)
            .Select(user => user.Id).Distinct().ToArrayAsync(cancellationToken);
    }

    public async Task<Result<IReadOnlyList<UserDeletionStateRow>>> ListDeletionStatesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        var ids = userIds.Distinct().Order().ToArray();
        if (ids.Length == 0) return Result.Success<IReadOnlyList<UserDeletionStateRow>>([]);
        var rows = await context.Users.AsNoTracking().Where(user => ids.Contains(user.Id))
            .Select(user => new UserDeletionStateRow(user.Id,
                user.Status == UserStatus.Deleted || user.DeletionLeaseId != null))
            .ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyList<UserDeletionStateRow>>(Complete(ids, rows));
    }

    public async Task<Result<IReadOnlyList<UserDeletionStateRow>>> LockDeletionStatesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        context.RequireTransaction();
        var ids = userIds.Distinct().Order().ToArray();
        if (ids.Length == 0) return Result.Success<IReadOnlyList<UserDeletionStateRow>>([]);
        // Claim updates this same row; module advisory locks cannot fence its lease write.
        var rows = await context.Database.SqlQuery<UserDeletionStateRow>($"""
            SELECT "Id" AS "UserId", ("Status" = 'Deleted' OR "DeletionLeaseId" IS NOT NULL) AS "HasDeletionStarted"
            FROM identity."AspNetUsers" WHERE "Id" = ANY({ids}) ORDER BY "Id" FOR SHARE
            """).ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyList<UserDeletionStateRow>>(Complete(ids, rows));
    }

    private static UserDeletionStateRow[] Complete(Guid[] ids, UserDeletionStateRow[] rows)
    {
        var states = rows.ToDictionary(row => row.UserId);
        return ids.Select(id => states.GetValueOrDefault(id) ?? new UserDeletionStateRow(id, true)).ToArray();
    }
}
