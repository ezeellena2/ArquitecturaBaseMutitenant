using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class AccountDeletionRepository(ApplicationDbContext context) : IAccountDeletionRepository
{
    public async Task<Guid?> ClaimAsync(Guid leaseId, DateTime nowUtc, DateTime leaseExpiresAtUtc, CancellationToken ct)
    {
        context.RequireTransaction();
        var claimed = await context.Database.SqlQuery<Guid>($"""
            WITH candidate AS (
                SELECT "Id" FROM identity."AspNetUsers"
                WHERE "DeletionScheduledForUtc" <= {nowUtc}
                  AND "Status" IN ('PendingDeletion', 'Suspended')
                  AND ("DeletionLeaseExpiresAtUtc" IS NULL OR "DeletionLeaseExpiresAtUtc" <= {nowUtc})
                ORDER BY "DeletionScheduledForUtc", "Id"
                FOR UPDATE SKIP LOCKED LIMIT 1)
            UPDATE identity."AspNetUsers" u
            SET "DeletionLeaseId" = {leaseId}, "DeletionLeaseExpiresAtUtc" = {leaseExpiresAtUtc}
            FROM candidate c WHERE u."Id" = c."Id" RETURNING u."Id" AS "Value"
            """).ToArrayAsync(ct);
        return claimed.Length == 0 ? null : claimed[0];
    }

    public async Task<bool> HasValidLeaseAsync(Guid userId, Guid leaseId, DateTime nowUtc, CancellationToken ct)
    {
        context.RequireTransaction();
        var valid = await context.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM identity."AspNetUsers"
            WHERE "Id" = {userId} AND "DeletionLeaseId" = {leaseId}
                AND "DeletionLeaseExpiresAtUtc" > {nowUtc} AND "DeletionScheduledForUtc" <= {nowUtc}
                AND "Status" IN ('PendingDeletion', 'Suspended') FOR NO KEY UPDATE
            """).ToArrayAsync(ct);
        return valid.Length == 1;
    }

    public async Task PurgeCredentialsAsync(Guid userId, CancellationToken ct)
    {
        context.RequireTransaction();
        var destinations = await context.LoginMethods.Where(row => row.UserId == userId
            && (row.Type == LoginMethodType.Email || row.Type == LoginMethodType.Phone))
            .Select(row => row.Value).ToArrayAsync(ct);
        context.LoginCodes.RemoveRange(await context.LoginCodes.Where(row => row.RequestedByUserId == userId || destinations.Contains(row.Destination)).ToArrayAsync(ct));
        context.ReauthTickets.RemoveRange(await context.ReauthTickets.Where(row => row.UserId == userId).ToArrayAsync(ct));
        context.LoginMethods.RemoveRange(await context.LoginMethods.Where(row => row.UserId == userId).ToArrayAsync(ct));
        context.UserClaims.RemoveRange(await context.UserClaims.Where(row => row.UserId == userId).ToArrayAsync(ct));
        context.UserLogins.RemoveRange(await context.UserLogins.Where(row => row.UserId == userId).ToArrayAsync(ct));
        context.UserTokens.RemoveRange(await context.UserTokens.Where(row => row.UserId == userId).ToArrayAsync(ct));
        var subject = userId.ToString("D");
        context.RemoveRange(await context.Set<OpenIddictEntityFrameworkCoreToken<Guid>>().Where(row => row.Subject == subject).ToArrayAsync(ct));
        context.RemoveRange(await context.Set<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().Where(row => row.Subject == subject).ToArrayAsync(ct));
    }
}
