using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using OpenIddict.EntityFrameworkCore.Models;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

[Collection(ApiTestGroup.Name)]
public sealed class AccountDeletionProcessingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Durable_claim_resumes_partial_cleanup_and_anonymizes_pending_or_suspended_once(bool suspended)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        await factory.Services.SeedDatabaseAsync(Ct);
        var clock = new FakeTimeProvider(factory.Services.GetRequiredService<TimeProvider>().GetUtcNow());
        var failure = new FailOnceAfterTenant();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IAccountDeletionParticipant>(failure);
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        Guid personalId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            personalId = (await services.GetRequiredService<IAccountDeletionTenantReader>().ListAsync(account.UserId, Ct))
                .Single(target => target.Kind == TenantKind.Personal).TenantId;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var user = await db.Users.SingleAsync(row => row.Id == account.UserId, ct);
                if (suspended) Assert.True(user.Suspend().IsSuccess);
                Assert.True(user.RequestDeletion("Prueba de eliminación", clock.GetUtcNow().UtcDateTime, 30,
                    suspended ? Guid.CreateVersion7() : null).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        clock.Advance(TimeSpan.FromDays(31));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var first = await scope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().ClaimNextAsync(Ct);
            Assert.Equal(account.UserId, first.Value!.UserId);
        }
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.Null((await scope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().ClaimNextAsync(Ct)).Value);
        clock.Advance(TimeSpan.FromMinutes(16));
        var worker = ActivatorUtilities.CreateInstance<AccountDeletionWorker>(host.Services);
        await worker.RunOnceAsync(Ct);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(suspended ? UserStatus.Suspended : UserStatus.PendingDeletion,
                (await db.Users.AsNoTracking().SingleAsync(row => row.Id == account.UserId, Ct)).Status);
            Assert.Equal(TenantStatus.Closed, (await db.Tenants.AsNoTracking().SingleAsync(row => row.Id == personalId, Ct)).Status);
            Assert.True(await db.LoginMethods.AnyAsync(row => row.UserId == account.UserId, Ct));
        }
        clock.Advance(TimeSpan.FromMinutes(16));
        await worker.RunOnceAsync(Ct);
        await worker.RunOnceAsync(Ct);
        await using var checkedScope = host.Services.CreateAsyncScope();
        var checkedDb = checkedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await checkedDb.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == account.UserId, Ct);
        Assert.Equal(UserStatus.Deleted, deleted.Status);
        Assert.Null(deleted.Email);
        Assert.Null(deleted.DeletionReason);
        Assert.NotNull(deleted.DeletedAtUtc);
        Assert.Null(deleted.DeletionLeaseId);
        Assert.Null(deleted.DeletionLeaseExpiresAtUtc);
        Assert.False(await checkedDb.LoginMethods.AnyAsync(row => row.UserId == account.UserId, Ct));
        Assert.False(await checkedDb.ReauthTickets.AnyAsync(row => row.UserId == account.UserId, Ct));
        Assert.False(await checkedDb.LoginCodes.AnyAsync(row => row.Destination == account.Email.Value, Ct));
        Assert.False(await checkedDb.Set<OpenIddictEntityFrameworkCoreToken<Guid>>().AnyAsync(row => row.Subject == account.UserId.ToString("D"), Ct));
        Assert.False(await checkedDb.UserClaims.AnyAsync(row => row.UserId == account.UserId, Ct));
        Assert.False(await checkedDb.UserLogins.AnyAsync(row => row.UserId == account.UserId, Ct));
        Assert.False(await checkedDb.UserTokens.AnyAsync(row => row.UserId == account.UserId, Ct));
        Assert.Single(await checkedDb.SecurityEvents.Where(row => row.ActorId == account.UserId
            && row.Type == ArquitecturaBaseMultitenant.Domain.Auditing.SecurityEventType.AccountDeleted).ToArrayAsync(Ct));
        Assert.Single(await checkedDb.OutboxMessages.Where(row => row.UserId == account.UserId
            && row.Status == ArquitecturaBaseMultitenant.Domain.Messaging.OutboxStatus.Pending).ToArrayAsync(Ct));
        var acceptances = await checkedDb.LegalAcceptances.Where(row => row.UserId == account.UserId).ToArrayAsync(Ct);
        Assert.Equal(2, acceptances.Length);
        Assert.All(acceptances, row => { Assert.Null(row.IpAddress); Assert.Null(row.UserAgent); });
    }

    private sealed class FailOnceAfterTenant : IAccountDeletionParticipant
    {
        private bool failed;
        public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Error>>([]);
        public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
        public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
        public Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
        {
            if (context.TenantId is null && !failed)
            {
                failed = true;
                throw new InvalidOperationException("Simulated participant failure.");
            }
            return Task.CompletedTask;
        }
    }
}
