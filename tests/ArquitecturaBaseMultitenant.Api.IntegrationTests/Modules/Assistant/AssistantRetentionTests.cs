using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
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
                db.Entry(row).Property(row => row.LastActivityAtUtc).CurrentValue = cutoff;
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
}
