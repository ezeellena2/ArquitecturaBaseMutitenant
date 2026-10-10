using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantDurableDeletionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Real_account_deletion_cleans_personal_exmember_bindings_and_notice_repeatably_preserving_other_owners()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var personal = await AssistantTestData.ActiveTenantAsync(client);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        using var quick = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, quick.StatusCode);
        using var content = JsonDocument.Parse(await quick.Content.ReadAsStringAsync(Ct));
        var personalConversation = content.RootElement.GetProperty("conversation").GetProperty("id").GetGuid();
        using var ownerClient = AssistantTestData.Client(factory);
        var other = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var organization = await OrganizationJourney.StageAsync(factory, other, "Durable deletion exmember", Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, organization, account.UserId, Ct);
        var exmemberConversation = await AssistantTestData.ConversationAsync(factory, organization, account.UserId, Access.Business);
        var survivingConversation = await AssistantTestData.ConversationAsync(factory, organization, other.UserId, Access.Business);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(organization);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                Assert.True((await db.Members.SingleAsync(row => row.Id == memberId, ct)).Remove().IsSuccess);
                db.Add(AssistantMessage.Create(exmemberConversation.Id, "Assistant", "Complete", "Private exmember message", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                db.Add(AssistantAttachment.Create(exmemberConversation.Id, "metadata-only-fixture.txt", "text/plain", "fixture-ledger-exmember", 1, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var requested = await AccountJourney.PostAsync(client, "/api/me/deletion", new { reason = "Baja durable de prueba" }, Ct);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.SingleAsync(row => row.Id == account.UserId, ct);
                db.Entry(user).Property(row => row.DeletionScheduledForUtc).CurrentValue = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-1);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var worker = ActivatorUtilities.CreateInstance<AccountDeletionWorker>(factory.Services);
        await worker.RunOnceAsync(Ct);
        await worker.RunOnceAsync(Ct);
        using var read = factory.Services.CreateScope();
        var provider = read.ServiceProvider;
        var database = provider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(UserStatus.Deleted, (await database.Users.AsNoTracking().SingleAsync(row => row.Id == account.UserId, Ct)).Status);
        Assert.Empty(await database.Set<AssistantNoticeAcceptance>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
        using (provider.GetRequiredService<ITenantScope>().Enter(personal))
        {
            Assert.False(await database.Set<AssistantConversation>().AnyAsync(row => row.Id == personalConversation, Ct));
            Assert.Empty(await database.Set<AssistantDurableOperation>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
        }
        using (provider.GetRequiredService<ITenantScope>().Enter(organization))
        {
            Assert.False(await database.Set<AssistantConversation>().AnyAsync(row => row.Id == exmemberConversation.Id, Ct));
            Assert.True(await database.Set<AssistantConversation>().AnyAsync(row => row.Id == survivingConversation.Id, Ct));
            Assert.False(await database.Set<AssistantMessage>().AnyAsync(row => row.ConversationId == exmemberConversation.Id, Ct));
            var ledger = Assert.Single(await database.Set<AssistantFileCleanup>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
            Assert.Equal("fixture-ledger-exmember", ledger.StorageKey);
        }
    }
}
