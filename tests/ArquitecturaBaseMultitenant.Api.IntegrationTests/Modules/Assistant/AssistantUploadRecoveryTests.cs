using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Files;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Files;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantUploadRecoveryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Crash_journal_removes_only_uncommitted_files_and_preserves_committed_files_in_real_scopes()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        var conversation = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Consumer);
        var files = factory.Services.GetRequiredService<IFileStore>();
        var maintenance = factory.Services.GetRequiredService<IFileStoreMaintenance>();
        await using var first = new MemoryStream([1, 2, 3]);
        await using var second = new MemoryStream([1, 2, 3]);
        var orphan = await files.PutAsync(new FileObject(Guid.NewGuid(), "image/png", 3, "assistant"), first, Ct);
        var committed = await files.PutAsync(new FileObject(Guid.NewGuid(), "image/png", 3, "assistant"), second, Ct);
        var now = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        using (var scope = factory.Services.CreateScope())
        {
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Add(AssistantAttachment.Create(conversation.Id, "foto.png", "image/png", committed.StorageKey, 3, now));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        // Crash after store write, respectively before commit and after commit but before filesystem confirmation.
        var root = factory.Services.GetRequiredService<IOptions<LocalFileStoreOptions>>().Value.RootPath;
        foreach (var key in new[] { orphan.StorageKey, committed.StorageKey })
            File.SetLastWriteTimeUtc(Path.Combine(root, key + ".assistant.unconfirmed"), now.AddHours(-25));
        Assert.Equal(2, (await maintenance.ListUnconfirmedAsync("assistant", now.AddHours(-24), Ct)).Count);
        await ActivatorUtilities.CreateInstance<AssistantRetentionWorker>(factory.Services).RunOnceAsync(Ct);
        Assert.Null(await files.OpenReadAsync(orphan.StorageKey, Ct));
        await using var preserved = await files.OpenReadAsync(committed.StorageKey, Ct);
        Assert.NotNull(preserved);
        Assert.Empty(await maintenance.ListUnconfirmedAsync("assistant", now.AddHours(-24), Ct));
    }
}
