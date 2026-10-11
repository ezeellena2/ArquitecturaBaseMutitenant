using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class UserDeletionStateReaderTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Grace_alone_is_not_started_but_an_expired_lease_deleted_and_missing_accounts_are_started()
    {
        var grace = ApplicationUser.Create("Grace", "es-AR", "America/Argentina/Buenos_Aires", NowUtc).Value;
        var leased = ApplicationUser.Create("Leased", "es-AR", "America/Argentina/Buenos_Aires", NowUtc).Value;
        var deleted = ApplicationUser.Create("Deleted", "es-AR", "America/Argentina/Buenos_Aires", NowUtc).Value;
        Assert.True(grace.RequestDeletion("Synthetic", NowUtc, 30).IsSuccess);
        Assert.True(leased.RequestDeletion("Synthetic", NowUtc.AddDays(-31), 30).IsSuccess);
        Assert.True(deleted.RequestDeletion("Synthetic", NowUtc.AddDays(-31), 30).IsSuccess);
        Assert.True(deleted.CompleteDeletion(NowUtc, "Removed").IsSuccess);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<ApplicationDbContext>();
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                context.Users.AddRange(grace, leased, deleted);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE identity."AspNetUsers" SET "DeletionLeaseId" = {Guid.CreateVersion7()},
                        "DeletionLeaseExpiresAtUtc" = {NowUtc.AddMinutes(-1)} WHERE "Id" = {leased.Id}
                    """, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var missing = Guid.CreateVersion7();
        await using var check = factory.Services.CreateAsyncScope();
        var reader = check.ServiceProvider.GetRequiredService<IUserStatusReader>();
        var ids = new[] { leased.Id, grace.Id, deleted.Id, missing, leased.Id };
        var query = await reader.ListDeletionStatesAsync(ids, Ct);
        Assert.True(query.IsSuccess);
        Assert.Equal(ids.Distinct().Order(), query.Value.Select(row => row.UserId));
        Assert.False(query.Value.Single(row => row.UserId == grace.Id).HasDeletionStarted);
        Assert.All(query.Value.Where(row => row.UserId != grace.Id), row => Assert.True(row.HasDeletionStarted));
        Assert.Null(check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction);
        var locked = await check.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            ct => reader.LockDeletionStatesAsync(ids, ct), CommitPolicy.OnSuccess, Ct);
        Assert.Equal(query.Value, locked.Value);
    }

    [Fact]
    public async Task Identity_fence_requires_the_callers_transaction_even_for_an_empty_batch()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IUserStatusReader>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.LockDeletionStatesAsync([], Ct));
    }

    [Fact]
    public async Task Shared_identity_fence_blocks_the_claims_strong_row_lock_until_the_writer_commits()
    {
        var user = ApplicationUser.Create("Writer subject", "es-AR", "America/Argentina/Buenos_Aires", NowUtc).Value;
        await using var writer = factory.Services.CreateAsyncScope();
        var services = writer.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var uow = services.GetRequiredService<IUnitOfWork>();
        await uow.ExecuteInTransactionAsync(ct =>
        {
            context.Users.Add(user);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        await using var claimant = factory.Services.CreateAsyncScope();
        async Task<Result> ClaimRowAsync(CancellationToken ct)
        {
            await claimant.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.SqlQuery<Guid>($"""
                SELECT "Id" AS "Value" FROM identity."AspNetUsers" WHERE "Id" = {user.Id} FOR UPDATE NOWAIT
                """).ToArrayAsync(ct);
            return Result.Success();
        }
        var claimantUow = claimant.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.ExecuteInTransactionAsync(async ct =>
        {
            var locked = await services.GetRequiredService<IUserStatusReader>().LockDeletionStatesAsync([user.Id], ct);
            Assert.True(locked.IsSuccess);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                claimantUow.ExecuteInTransactionAsync(ClaimRowAsync, CommitPolicy.OnSuccess, ct));
            var blocked = Assert.IsType<PostgresException>(failure.InnerException);
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, blocked.SqlState);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True((await claimantUow.ExecuteInTransactionAsync(ClaimRowAsync, CommitPolicy.OnSuccess, Ct)).IsSuccess);
    }
}
