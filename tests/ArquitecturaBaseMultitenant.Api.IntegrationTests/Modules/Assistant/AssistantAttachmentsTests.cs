using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Files;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantAttachmentsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=");

    [Fact]
    public async Task Upload_is_durable_private_binary_and_deleted_through_the_cleanup_ledger()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        var conversation = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Consumer);
        var key = Guid.NewGuid();
        const string boundary = "e8b-durable-upload-boundary";
        using var uploaded = await UploadAsync(client, conversation.Id, conversation.Version, key, Png, "image/png", boundary);
        var body = await uploaded.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var value = JsonDocument.Parse(body);
        var id = value.RootElement.GetProperty("id").GetGuid();
        var version = value.RootElement.GetProperty("version").GetUInt32();
        Assert.NotEqual(0u, version);
        Assert.DoesNotContain("storageKey", body, StringComparison.OrdinalIgnoreCase);
        using var replay = await UploadAsync(client, conversation.Id, conversation.Version, key, Png, "image/png", "new-browser-boundary");
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(body, await replay.Content.ReadAsStringAsync(Ct));
        using var changed = await UploadAsync(client, conversation.Id, conversation.Version, key, [.. Png, 0], "image/png", boundary);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        using var download = await client.GetAsync($"/api/me/assistant/conversations/{conversation.Id}/attachments/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.True(download.Headers.CacheControl!.NoStore);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        using var other = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, other, Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(other);
        using var foreign = await other.GetAsync($"/api/me/assistant/conversations/{conversation.Id}/attachments/{id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        string storageKey;
        using (var scope = factory.Services.CreateScope())
        {
            using var scopeTenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.Set<AssistantAttachment>().AsNoTracking().SingleAsync(row => row.Id == id, Ct);
            storageKey = row.StorageKey;
        }
        using var removed = await client.DeleteAsync($"/api/me/assistant/conversations/{conversation.Id}/attachments/{id}?version={version}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var expiredReplay = await UploadAsync(client, conversation.Id, conversation.Version, key, Png, "image/png", boundary);
        Assert.Equal(HttpStatusCode.NotFound, expiredReplay.StatusCode);
        var worker = ActivatorUtilities.CreateInstance<AssistantRetentionWorker>(factory.Services);
        await worker.RunOnceAsync(Ct);
        Assert.Null(await factory.Services.GetRequiredService<IFileStore>().OpenReadAsync(storageKey, Ct));
    }

    [Fact]
    public async Task Forged_mime_and_one_byte_over_limit_are_rejected_before_persisting()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        var conversation = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Consumer);
        using var wrong = await UploadAsync(client, conversation.Id, conversation.Version, Guid.NewGuid(), Encoding.UTF8.GetBytes("<html>not an image</html>"), "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var exact = new byte[10485760];
        Png.CopyTo(exact, 0);
        using var accepted = await UploadAsync(client, conversation.Id, conversation.Version, Guid.NewGuid(), exact, "image/png");
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using var large = await UploadAsync(client, conversation.Id, conversation.Version, Guid.NewGuid(), [.. exact, 0], "image/png");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
    }

    [Fact]
    public async Task More_than_five_attachments_return_the_published_code_on_the_field()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        var conversation = await AssistantTestData.ConversationAsync(factory, tenant, account.UserId, Access.Consumer);
        var body = JsonSerializer.Serialize(new
        {
            text = "Revisá estos archivos.",
            attachmentIds = Enumerable.Range(0, AssistantTechnicalLimits.MaxAttachments + 1).Select(_ => Guid.NewGuid()),
            conversationVersion = conversation.Version,
        });
        using var response = await AssistantDurableConversationTests.PostAsync(client, $"/api/me/assistant/conversations/{conversation.Id}/messages", body, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(AssistantErrors.AttachmentTooManyCode, problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("attachmentIds", out _));
    }

    [Fact]
    public async Task Uploading_beyond_the_pending_limit_is_rejected_without_storing_files_and_sending_a_message_allows_new_uploads()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("consumer");
        var conversation = await session.ConversationAsync();
        var uploadedIds = new List<Guid>(AssistantTechnicalLimits.MaxUnboundAttachmentsPerConversation);
        var replayKey = Guid.Empty;
        string? replayBody = null;
        for (var index = 0; index < AssistantTechnicalLimits.MaxUnboundAttachmentsPerConversation; index++)
        {
            var key = Guid.NewGuid();
            using var uploaded = await UploadAsync(session.Client, conversation.Id, conversation.Version, key, Png, "image/png", root: session.Path);
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
            var body = await uploaded.Content.ReadAsStringAsync(Ct);
            if (index == 0)
            {
                replayKey = key;
                replayBody = body;
            }
            var summary = JsonSerializer.Deserialize<AssistantAttachmentSummaryResponse>(body, AssistantTurnTestSession.Json)!;
            uploadedIds.Add(summary.Id);
        }

        var storeRoot = session.Host.Services.GetRequiredService<IOptions<LocalFileStoreOptions>>().Value.RootPath;
        var filesBeforeRejected = Directory.Exists(storeRoot) ? Directory.GetFiles(storeRoot).Length : 0;

        // La subida N+1 pendiente en la misma conversación se rechaza sin guardar archivo ni fila nueva.
        using (var rejected = await UploadAsync(session.Client, conversation.Id, conversation.Version, Guid.NewGuid(), Png, "image/png", root: session.Path))
        {
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(Ct));
            Assert.Equal(AssistantErrors.AttachmentTooManyPendingCode, problem.RootElement.GetProperty("code").GetString());
            Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("file", out _));
        }

        var filesAfterRejected = Directory.Exists(storeRoot) ? Directory.GetFiles(storeRoot).Length : 0;
        Assert.Equal(filesBeforeRejected, filesAfterRejected);
        Assert.Empty(await session.Host.Services.GetRequiredService<IFileStoreMaintenance>()
            .ListUnconfirmedAsync("assistant", session.Host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(1), Ct));
        using (var scope = session.Host.Services.CreateScope())
        {
            using var scopeTenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
            var count = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantAttachment>()
                .CountAsync(row => row.ConversationId == conversation.Id && row.MessageId == null, Ct);
            Assert.Equal(AssistantTechnicalLimits.MaxUnboundAttachmentsPerConversation, count);
        }

        // El replay idempotente de una subida ya guardada sigue respondiendo 201 aunque la conversación esté en el tope.
        using (var replay = await UploadAsync(session.Client, conversation.Id, conversation.Version, replayKey, Png, "image/png", root: session.Path))
        {
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal(replayBody, await replay.Content.ReadAsStringAsync(Ct));
        }

        // Enviar el mensaje asocia los adjuntos y permite nuevas subidas.
        session.Provider.Release();
        var sendBody = JsonSerializer.Serialize(new
        {
            text = "Revisá estos archivos.",
            attachmentIds = uploadedIds,
            conversationVersion = conversation.Version,
        });
        using (var sendRequest = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages", sendBody, Guid.NewGuid()))
        using (var sendResponse = await session.OpenStreamAsync(sendRequest, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
            using var stream = await AssistantSseTestReader.OpenAsync(sendResponse, Ct);
            AssistantSseTestReader.Frame frame;
            do frame = await stream.NextAsync(Ct); while (frame.Event is not ("done" or "error"));
            Assert.True(frame.Event == "done", frame.Data.ToString());
        }

        var updated = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(updated);
        using var nextUpload = await UploadAsync(session.Client, conversation.Id, updated.Conversation.Version, Guid.NewGuid(), Png, "image/png", root: session.Path);
        Assert.Equal(HttpStatusCode.Created, nextUpload.StatusCode);
    }

    internal static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid conversation, uint version, Guid key, byte[] bytes, string mediaType, string boundary = "assistant-fixture-boundary", string root = "/api/me/assistant")
    {
        using var content = new MultipartFormDataContent(boundary);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Add(file, "file", "árbol.png");
        content.Add(new StringContent(System.Xml.XmlConvert.ToString(version)), "conversationVersion");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{root}/conversations/{conversation}/attachments") { Content = content };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await client.SendAsync(request, Ct);
    }
}
