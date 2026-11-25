using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// 0064 §6 (B14): una conversación con un mensaje que escribió una versión más nueva, con un rol o un estado que esta no
/// conoce, sale en el listado sin título y con <c>available: false</c>, no se abre (409) y se puede borrar, en las tres bases.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantUnreadableConversationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer", "Role")]
    [InlineData("business", "Status")]
    [InlineData("platform", "Role")]
    [InlineData("platform", "Status")]
    public async Task A_conversation_a_newer_version_wrote_lists_without_title_does_not_open_and_can_be_deleted(string access, string column)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(access, workerEnabled: false);
        var readable = await session.ConversationAsync();
        var conversation = await RenameAsync(session, await session.ConversationAsync(), "Título que no se muestra");
        await WriteFutureMessageAsync(session, conversation.Id, column);

        using (var list = await session.Client.GetAsync(session.Path + "/conversations", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync(Ct));
            var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var unreadable = Assert.Single(items, item => item.GetProperty("id").GetGuid() == conversation.Id);
            Assert.False(unreadable.GetProperty("available").GetBoolean());
            Assert.Equal(JsonValueKind.Null, unreadable.GetProperty("title").ValueKind);
            Assert.True(Assert.Single(items, item => item.GetProperty("id").GetGuid() == readable.Id).GetProperty("available").GetBoolean());
        }
        using (var opened = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}", Ct))
            await AssistantTurnTestSession.AssertProblemAsync(opened, HttpStatusCode.Conflict, AssistantErrors.ConversationUnavailableCode);
        Assert.NotNull(await session.HistoryAsync(readable.Id, Ct));

        using (var deleted = await session.Client.DeleteAsync(session.Path + $"/conversations/{conversation.Id:D}?version={conversation.Version}", Ct))
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}", Ct);
        await AssistantTurnTestSession.AssertProblemAsync(missing, HttpStatusCode.NotFound, AssistantErrors.ConversationNotFoundCode);
        Assert.Equal(0, session.Provider.Calls);
    }

    private static async Task<AssistantConversationSummaryResponse> RenameAsync(AssistantTurnTestSession session,
        AssistantConversationSummaryResponse conversation, string title)
    {
        using var renamed = await session.Client.PutAsJsonAsync(session.Path + $"/conversations/{conversation.Id:D}",
            new { title, isSaved = true, version = conversation.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var version = (await renamed.Content.ReadFromJsonAsync<VersionResponse>(AssistantTurnTestSession.Json, Ct))!.Version;
        return conversation with { Title = title, Version = version };
    }

    // Lo que escribiría una versión más nueva: un mensaje válido para esta, con su rol o su estado cambiado en la base.
    private static async Task WriteFutureMessageAsync(AssistantTurnTestSession session, Guid conversationId, string column)
    {
        using var scope = session.Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var platform = session.Access == "platform";
        using var tenant = platform ? null : services.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var id = Guid.Empty;
        var written = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            if (platform)
            {
                var message = PlatformAssistantMessage.Create(conversationId, "User", "Complete", "Mensaje de una versión más nueva", nowUtc);
                db.Add(message);
                id = message.Id;
            }
            else
            {
                var message = AssistantMessage.Create(conversationId, "User", "Complete", "Mensaje de una versión más nueva", nowUtc);
                db.Add(message);
                id = message.Id;
            }
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(written.IsSuccess, written.Error.Code);
        var table = platform ? "platform.\"PlatformAssistantMessages\"" : "tenant.\"AssistantMessages\"";
        var sql = column == "Role"
            ? "UPDATE " + table + " SET \"Role\" = 'Futuro' WHERE \"Id\" = {0}"
            : "UPDATE " + table + " SET \"Status\" = 'Futuro' WHERE \"Id\" = {0}";
        var changed = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            Assert.Equal(1, await services.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync(sql, [id], ct));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(changed.IsSuccess, changed.Error.Code);
    }
}
