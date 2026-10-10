using System.Data.Common;
using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantNoticeSeedRaceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Existing_operator_notice_and_restart_seed_with_verified_phone_finish_without_deadlock()
    {
        const string email = "notice-seed-operator@example.test";
        const string phone = "+5491134567890";
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var barrier = new AccountBarrier();
        using var host = isolated.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:PlatformOwner:Email", email).UseSetting("Seed:PlatformOwner:Phone", phone);
            builder.ConfigureTestServices(services => services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(barrier)));
        });
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await host.Services.SeedDatabaseAsync(Ct);
        Guid operatorId;
        object[] documents;
        using (var setup = host.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Users.AsNoTracking().SingleAsync(row => row.Email == email, Ct);
            operatorId = owner.Id;
            Assert.True(owner.IsPlatformOperator);
            Assert.False(await db.UserTenantAccesses.AnyAsync(row => row.UserId == operatorId, Ct));
            var verified = await db.LoginMethods.AsNoTracking().SingleAsync(row => row.UserId == operatorId && row.Type == LoginMethodType.Phone, Ct);
            Assert.Equal(phone, verified.Value);
            Assert.NotNull(verified.VerifiedAtUtc);
            var current = await db.LegalDocuments.AsNoTracking().Where(row => row.Version == 2).ToArrayAsync(Ct);
            Assert.Equal(2, current.Length);
            documents = current.Select(row => (object)new { id = row.Id, version = row.Version }).ToArray();
        }
        // Use the operator's actual OTP/cookie/OIDC path; the request keeps production CurrentUser and DI.
        using (var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email }, Ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(host.Services, email, Ct);
        using (var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
                   new { email, code, returnUrl = "/connect/authorize?access=platform" }, Ct))
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using (var accepted = await AccountJourney.PostAsync(client, "/api/legal/accept", new { documents }, Ct))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        using var seedScope = host.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        barrier.Arm(seedDb.ContextId.InstanceId, operatorId);
        var restarting = seedScope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(Ct);
        Task<HttpResponseMessage>? accepting = null;
        try
        {
            await barrier.SeedHoldsAccount.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            accepting = AssistantNoticeTests.SendAsync(client, "1", Guid.NewGuid(), "/api/platform/assistant/notice");
            // The old notice path already holds both legal locks when it reaches Account; the corrected path does not.
            await barrier.NoticeRequestsAccount.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        }
        finally { barrier.ReleaseSeed.TrySetResult(); }
        Assert.NotNull(accepting);
        await Task.WhenAll(restarting, accepting).WaitAsync(TimeSpan.FromSeconds(30), Ct);
        using var response = await accepting;
        Assert.True(response.StatusCode == HttpStatusCode.NoContent,
            $"Notice should commit204; got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        Assert.NotEqual(barrier.SeedBackendId, barrier.NoticeBackendId);
        using var read = host.Services.CreateScope();
        var dbAfter = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var acceptance = await dbAfter.Set<AssistantNoticeAcceptance>().AsNoTracking().SingleAsync(row => row.OwnerUserId == operatorId, Ct);
        Assert.Equal("1", acceptance.NoticeVersion);
        Assert.Equal(Access.Platform, acceptance.Access);
        Assert.Equal(2, await dbAfter.LegalDocuments.CountAsync(row => row.Version == 2, Ct));
        Assert.NotNull((await dbAfter.LoginMethods.AsNoTracking().SingleAsync(row => row.UserId == operatorId && row.Type == LoginMethodType.Phone, Ct)).VerifiedAtUtc);
    }

    private sealed class AccountBarrier : DbCommandInterceptor
    {
        private Guid? seedContext;
        private string? accountKey;
        internal int SeedBackendId { get; private set; }
        internal int NoticeBackendId { get; private set; }
        internal TaskCompletionSource SeedHoldsAccount { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource NoticeRequestsAccount { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseSeed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Arm(Guid contextId, Guid owner) { seedContext = contextId; accountKey = AdvisoryLockKeys.Account(owner); }

        private bool IsAccount(DbCommand command) => accountKey is not null
            && command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)
            && command.Parameters.Cast<DbParameter>().Any(value => Equals(value.Value, accountKey));

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (IsAccount(command) && eventData.Context?.ContextId.InstanceId != seedContext)
            {
                NoticeBackendId = ((NpgsqlConnection)command.Connection!).ProcessID;
                NoticeRequestsAccount.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (IsAccount(command) && eventData.Context?.ContextId.InstanceId == seedContext)
            {
                SeedBackendId = ((NpgsqlConnection)command.Connection!).ProcessID;
                SeedHoldsAccount.TrySetResult();
                await ReleaseSeed.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
