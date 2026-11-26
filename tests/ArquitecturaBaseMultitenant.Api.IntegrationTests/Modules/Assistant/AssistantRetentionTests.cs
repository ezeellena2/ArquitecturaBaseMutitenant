using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantRetentionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Retention_rechecks_save_and_activity_under_lock_and_stages_files_atomically_without_gates()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        var expired = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Consumer);
        var saved = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Consumer);
        var active = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Consumer);
        var cutoff = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-30);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var scopeTenant = services.GetRequiredService<ITenantScope>().Enter(tenant);
        var db = services.GetRequiredService<ApplicationDbContext>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            foreach (var id in new[] { expired.Id, saved.Id })
            {
                var row = await db.Set<AssistantConversation>().SingleAsync(row => row.Id == id, ct);
                row.Activity.Touch(cutoff);
                db.Entry(row).Property(row => row.CreatedAtUtc).CurrentValue = cutoff.AddDays(-1);
            }
            db.Add(AssistantAttachment.Create(expired.Id, "private-file.pdf", "application/pdf", Guid.NewGuid().ToString("N"), 12, cutoff));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        db.ChangeTracker.Clear();
        var candidates = await services.GetRequiredService<IAssistantRetentionRepository>().ListExpiredAsync(cutoff, null, Ct);
        Assert.Contains(candidates, row => row.Id == expired.Id);
        Assert.Contains(candidates, row => row.Id == saved.Id);
        Assert.DoesNotContain(candidates, row => row.Id == active.Id);
        // A save occurring after candidate enumeration must win the same conversation lock.
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var row = await db.Set<AssistantConversation>().SingleAsync(row => row.Id == saved.Id, ct);
            row.Update(null, true, factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        db.ChangeTracker.Clear();
        var service = services.GetRequiredService<IAssistantRetentionService>();
        Assert.Equal(0, (await service.PurgeAsync(candidates.Single(row => row.Id == saved.Id), cutoff, Ct)).Value);
        Assert.Equal(1, (await service.PurgeAsync(candidates.Single(row => row.Id == expired.Id), cutoff, Ct)).Value);
        Assert.Equal(0, (await service.PurgeAsync(candidates.Single(row => row.Id == expired.Id), cutoff, Ct)).Value);
        Assert.False(await db.Set<AssistantConversation>().AnyAsync(row => row.Id == expired.Id, Ct));
        Assert.True(await db.Set<AssistantConversation>().AnyAsync(row => row.Id == saved.Id, Ct));
        Assert.True(await db.Set<AssistantConversation>().AnyAsync(row => row.Id == active.Id, Ct));
        Assert.Single(await db.Set<AssistantFileCleanup>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Retention_of_an_organization_conversation_removes_its_proposal_and_keeps_a_saved_one()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, account, "Retención de propuestas", Ct);
        var expired = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Business);
        var saved = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Business);
        var values = new AssistantProposalValues(null, [AssistantTestData.Written("name", "Ventas")]);
        var removed = await AssistantTestData.ProposalAsync(factory, tenant, expired.Id, account.UserId, "roles.create", 1, values);
        var kept = await AssistantTestData.ProposalAsync(factory, tenant, saved.Id, account.UserId, "roles.create", 1, values);
        var cutoff = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-30);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var scopeTenant = services.GetRequiredService<ITenantScope>().Enter(tenant);
        var db = services.GetRequiredService<ApplicationDbContext>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            foreach (var id in new[] { expired.Id, saved.Id })
            {
                var row = await db.Set<AssistantConversation>().SingleAsync(row => row.Id == id, ct);
                row.Activity.Touch(cutoff);
                db.Entry(row).Property(row => row.CreatedAtUtc).CurrentValue = cutoff.AddDays(-1);
                if (id == saved.Id) row.Update(null, true, cutoff);
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        db.ChangeTracker.Clear();
        var candidates = await services.GetRequiredService<IAssistantRetentionRepository>().ListExpiredAsync(cutoff, null, Ct);
        var service = services.GetRequiredService<IAssistantRetentionService>();
        foreach (var candidate in candidates.Where(row => row.Id == expired.Id || row.Id == saved.Id))
            Assert.True((await service.PurgeAsync(candidate, cutoff, Ct)).IsSuccess);
        db.ChangeTracker.Clear();
        Assert.False(await db.Set<AssistantConversation>().AnyAsync(row => row.Id == expired.Id, Ct));
        Assert.False(await db.Set<AssistantProposal>().AnyAsync(row => row.Id == removed.Id, Ct));
        Assert.True(await db.Set<AssistantProposal>().AnyAsync(row => row.Id == kept.Id, Ct));
    }

    [Fact]
    public async Task Account_deletion_removes_the_proposals_of_its_organization_conversations()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        using var ownerClient = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Baja con propuestas", Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenant, account.UserId, Ct);
        var values = new AssistantProposalValues(null, [AssistantTestData.Written("name", "Ventas")]);
        var leaving = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Business);
        var staying = await AssistantTestData.ConversationAsync(factory, tenant, owner.UserId, Access.Business);
        var removed = await AssistantTestData.ProposalAsync(factory, tenant, leaving.Id, account.UserId, "roles.create", 1, values);
        var kept = await AssistantTestData.ProposalAsync(factory, tenant, staying.Id, owner.UserId, "roles.create", 1, values);
        using (var requested = await AccountJourney.PostAsync(client, "/api/me/deletion", new { reason = "Baja con propuestas del Asistente" }, Ct))
            Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.SingleAsync(row => row.Id == account.UserId, ct);
                db.Entry(user).Property(row => row.DeletionScheduledForUtc).CurrentValue =
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-1);
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
        using (provider.GetRequiredService<ITenantScope>().Enter(tenant))
        {
            Assert.False(await database.Set<AssistantConversation>().AnyAsync(row => row.Id == leaving.Id, Ct));
            Assert.False(await database.Set<AssistantProposal>().AnyAsync(row => row.Id == removed.Id, Ct));
            Assert.True(await database.Set<AssistantProposal>().AnyAsync(row => row.Id == kept.Id, Ct));
        }
    }
}
