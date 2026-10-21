using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantConversationRuntimeVersionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=");

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    [InlineData("platform")]
    public async Task Deleting_while_generating_removes_the_image_and_stops_its_durable_work(string access)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(access);
        var conversation = await CreateConversationAsync(session);
        var attachment = await UploadAsync(session, conversation.Id, conversation.Version);
        var uploaded = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(uploaded);
        var body = JsonSerializer.Serialize(new { text = "Borrar este turno activo con su imagen.", attachmentIds = new[] { attachment.Id }, conversationVersion = uploaded.Conversation.Version });
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages", body, Guid.NewGuid());
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var start = await stream.NextAsync(Ct);
        Assert.Equal("start", start.Event);
        var turnId = start.Data.GetProperty("turnId").GetGuid();
        Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
        var opened = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(opened);
        Assert.Equal(AssistantMessageStatus.Generating, opened.Messages.Items[^1].Status);
        Assert.False(session.Provider.IsReleased);
        using var deleted = await session.Client.DeleteAsync(session.Path + $"/conversations/{conversation.Id:D}?version={opened.Conversation.Version}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missingFile = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}/attachments/{attachment.Id:D}", Ct);
        await AssistantTurnTestSession.AssertProblemAsync(missingFile, HttpStatusCode.NotFound, "Assistant.Attachment.NotFound");
        using var missingConversation = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}", Ct);
        await AssistantTurnTestSession.AssertProblemAsync(missingConversation, HttpStatusCode.NotFound, "Assistant.Conversation.NotFound");
        using var scope = session.Host.Services.CreateScope();
        using var tenant = access == "platform" ? null : scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var work = await db.Set<AssistantTurnWork>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == turnId, Ct);
        Assert.True(work is null || work.IsTerminal || work.StopRequested);
        if (access == "platform")
            Assert.True(await db.Set<PlatformAssistantFileCleanup>().AnyAsync(row => row.OwnerUserId == session.Account.UserId, Ct));
        else
            Assert.True(await db.Set<AssistantFileCleanup>().AnyAsync(row => row.OwnerUserId == session.Account.UserId, Ct));
        session.Provider.Release();
    }

    [Theory]
    [InlineData("consumer", false)]
    [InlineData("business", false)]
    [InlineData("platform", false)]
    [InlineData("consumer", true)]
    [InlineData("business", true)]
    [InlineData("platform", true)]
    public async Task Completing_after_delete_is_open_preserves_its_version_but_a_manual_rename_still_conflicts(
        string access, bool renameWhileGenerating)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(access);
        var conversation = await CreateConversationAsync(session);
        var attachment = await UploadAsync(session, conversation.Id, conversation.Version);
        var uploaded = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(uploaded);
        var body = JsonSerializer.Serialize(new
        {
            text = "Borrar la conversación después de esta respuesta con imagen.",
            attachmentIds = new[] { attachment.Id },
            conversationVersion = uploaded.Conversation.Version,
        });
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", body, Guid.NewGuid());
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("start", (await stream.NextAsync(Ct)).Event);
        Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
        var opened = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(opened);
        Assert.Equal(AssistantMessageStatus.Generating, opened.Messages.Items[^1].Status);
        Assert.False(session.Provider.IsReleased);
        if (renameWhileGenerating)
        {
            using var renamed = await session.Client.PutAsJsonAsync(session.Path + $"/conversations/{conversation.Id:D}",
                new { title = "Nombre elegido en otra pestaña", isSaved = true, version = opened.Conversation.Version }, Ct);
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        }
        session.Provider.Release();
        var done = await stream.TerminalAsync(Ct);
        Assert.Equal("done", done.Event);
        var completed = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(completed);
        Assert.Equal(completed.Conversation.Version, done.Data.GetProperty("conversationVersion").GetUInt32());
        Assert.Null(completed.ActiveTurnId);
        Assert.Equal(AssistantMessageStatus.Complete, completed.Messages.Items[^1].Status);
        Assert.True(completed.Conversation.LastActivityAtUtc >= opened.Conversation.LastActivityAtUtc);
        if (renameWhileGenerating)
        {
            Assert.NotEqual(opened.Conversation.Version, completed.Conversation.Version);
            Assert.Equal("Nombre elegido en otra pestaña", completed.Conversation.Title);
            Assert.Equal(0, session.Provider.TitleCalls);
            using var conflict = await session.Client.DeleteAsync(session.Path + $"/conversations/{conversation.Id:D}?version={opened.Conversation.Version}", Ct);
            await AssistantTurnTestSession.AssertProblemAsync(conflict, HttpStatusCode.Conflict, "General.ConcurrencyConflict");
            Assert.NotNull(await session.HistoryAsync(conversation.Id, Ct));
            using var preserved = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}/attachments/{attachment.Id:D}", Ct);
            Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
            Assert.Equal(Png, await preserved.Content.ReadAsByteArrayAsync(Ct));
        }
        else
        {
            Assert.Equal("Título de prueba", completed.Conversation.Title);
            Assert.Equal(1, session.Provider.TitleCalls);
            Assert.Equal(opened.Conversation.Version, completed.Conversation.Version);
        }
        // El conflicto no borra contenido; cerrar y abrir de nuevo adopta la
        // nueva lectura explícita. No hay rebase ni reintento automático.
        var deleteVersion = renameWhileGenerating ? completed.Conversation.Version : opened.Conversation.Version;
        using var deleted = await session.Client.DeleteAsync(session.Path + $"/conversations/{conversation.Id:D}?version={deleteVersion}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missingConversation = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}", Ct);
        await AssistantTurnTestSession.AssertProblemAsync(missingConversation, HttpStatusCode.NotFound, "Assistant.Conversation.NotFound");
        using var missingFile = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}/attachments/{attachment.Id:D}", Ct);
        await AssistantTurnTestSession.AssertProblemAsync(missingFile, HttpStatusCode.NotFound, "Assistant.Attachment.NotFound");
    }

    private static async Task<AssistantConversationSummaryResponse> CreateConversationAsync(AssistantTurnTestSession session)
    {
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/conversations", "{}", Guid.NewGuid());
        using var response = await session.Client.SendAsync(request, Ct);
        var failures = session.Provider.Logs.Snapshot().Where(value => value.StartsWith("System.", StringComparison.Ordinal)
                || value.StartsWith("Npgsql.", StringComparison.Ordinal))
            .Select(value => value.Split('\n', 2)[0].Trim()).Distinct().TakeLast(3);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Creating the conversation must respond 201, received {(int)response.StatusCode}: {string.Join(" | ", failures)}");
        return (await response.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json, Ct))!;
    }

    private static async Task<AssistantAttachmentSummaryResponse> UploadAsync(AssistantTurnTestSession session, Guid conversationId, uint version)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "propia.png");
        form.Add(new StringContent(System.Xml.XmlConvert.ToString(version)), "conversationVersion");
        using var request = new HttpRequestMessage(HttpMethod.Post, session.Path + $"/conversations/{conversationId:D}/attachments") { Content = form };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await session.Client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssistantAttachmentSummaryResponse>(AssistantTurnTestSession.Json, Ct))!;
    }
}
