using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantAttachmentsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=");

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

    internal static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid conversation, uint version, Guid key, byte[] bytes, string mediaType, string boundary = "assistant-fixture-boundary")
    {
        using var content = new MultipartFormDataContent(boundary);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Add(file, "file", "árbol.png");
        content.Add(new StringContent(System.Xml.XmlConvert.ToString(version)), "conversationVersion");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/me/assistant/conversations/{conversation}/attachments") { Content = content };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await client.SendAsync(request, Ct);
    }
}
