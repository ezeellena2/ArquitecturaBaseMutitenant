using System.Net;
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

/// <summary>Un adjunto ligado a un mensaje no impide borrar ni vencer su conversación.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantLinkedAttachmentDeletionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Organization_conversation_with_a_linked_attachment_is_deleted_and_expires()
    {
        using var client = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Linked attachments", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        await AssistantTestData.EnableAsync(factory, tenant);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/organization/assistant/notice");
        var deleted = await AssistantTestData.ConversationAsync(factory, tenant, owner.UserId, Access.Business);
        var expired = await AssistantTestData.ConversationAsync(factory, tenant, owner.UserId, Access.Business);
        var cutoff = Now().AddDays(-30);
        await LinkAsync(tenant, deleted.Id, expiresAtUtc: null);
        await LinkAsync(tenant, expired.Id, cutoff);

        using var response = await client.DeleteAsync($"/api/organization/assistant/conversations/{deleted.Id}?version={deleted.Version}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenant);
        var candidate = Assert.Single(await services.GetRequiredService<IAssistantRetentionRepository>().ListExpiredAsync(cutoff, null, Ct),
            row => row.Id == expired.Id);
        Assert.Equal(1, (await services.GetRequiredService<IAssistantRetentionService>().PurgeAsync(candidate, cutoff, Ct)).Value);
        var db = services.GetRequiredService<ApplicationDbContext>();
        var conversations = new[] { deleted.Id, expired.Id };
        Assert.False(await db.Set<AssistantConversation>().AnyAsync(row => conversations.Contains(row.Id), Ct));
        Assert.False(await db.Set<AssistantMessage>().AnyAsync(row => conversations.Contains(row.ConversationId), Ct));
        Assert.False(await db.Set<AssistantAttachment>().AnyAsync(row => conversations.Contains(row.ConversationId), Ct));
        Assert.Equal(2, await db.Set<AssistantFileCleanup>().CountAsync(row => row.OwnerUserId == owner.UserId, Ct));
    }

    [Fact]
    public async Task Platform_conversation_with_a_linked_attachment_is_deleted_and_expires()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/platform/assistant/notice");
        var cutoff = Now().AddDays(-30);
        var deleted = await PlatformLinkAsync(account.UserId, expiresAtUtc: null);
        var expired = await PlatformLinkAsync(account.UserId, cutoff);

        using var response = await client.DeleteAsync($"/api/platform/assistant/conversations/{deleted.Id}?version={deleted.Version}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var candidate = Assert.Single(await services.GetRequiredService<IAssistantRetentionRepository>().ListExpiredAsync(cutoff, null, Ct),
            row => row.Id == expired.Id);
        Assert.Equal(1, (await services.GetRequiredService<IAssistantRetentionService>().PurgeAsync(candidate, cutoff, Ct)).Value);
        var db = services.GetRequiredService<ApplicationDbContext>();
        var conversations = new[] { deleted.Id, expired.Id };
        Assert.False(await db.Set<PlatformAssistantConversation>().AnyAsync(row => conversations.Contains(row.Id), Ct));
        Assert.False(await db.Set<PlatformAssistantMessage>().AnyAsync(row => conversations.Contains(row.ConversationId), Ct));
        Assert.False(await db.Set<PlatformAssistantAttachment>().AnyAsync(row => conversations.Contains(row.ConversationId), Ct));
        Assert.Equal(2, await db.Set<PlatformAssistantFileCleanup>().CountAsync(row => row.OwnerUserId == account.UserId, Ct));
    }

    private DateTime Now() => factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

    private async Task LinkAsync(Guid tenant, Guid conversationId, DateTime? expiresAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenant);
        var db = services.GetRequiredService<ApplicationDbContext>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var message = AssistantMessage.Create(conversationId, "User", "Complete", "Revisá la planilla.", Now());
            var attachment = AssistantAttachment.Create(conversationId, "planilla.pdf", "application/pdf", Guid.NewGuid().ToString("N"), 12, Now());
            attachment.Bind(message.Id);
            db.AddRange(message, attachment);
            if (expiresAtUtc is { } cutoff)
            {
                var row = await db.Set<AssistantConversation>().SingleAsync(row => row.Id == conversationId, ct);
                db.Entry(row).Property(row => row.LastActivityAtUtc).CurrentValue = cutoff;
                db.Entry(row).Property(row => row.CreatedAtUtc).CurrentValue = cutoff.AddDays(-1);
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task<PlatformAssistantConversation> PlatformLinkAsync(Guid owner, DateTime? expiresAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var conversation = PlatformAssistantConversation.Create(owner, Now());
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var message = PlatformAssistantMessage.Create(conversation.Id, "User", "Complete", "Revisá la planilla.", Now());
            var attachment = PlatformAssistantAttachment.Create(conversation.Id, "planilla.pdf", "application/pdf", Guid.NewGuid().ToString("N"), 12, Now());
            attachment.Bind(message.Id);
            db.AddRange(conversation, message, attachment);
            if (expiresAtUtc is { } cutoff)
            {
                db.Entry(conversation).Property(row => row.LastActivityAtUtc).CurrentValue = cutoff;
                db.Entry(conversation).Property(row => row.CreatedAtUtc).CurrentValue = cutoff.AddDays(-1);
            }
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return conversation;
    }
}
