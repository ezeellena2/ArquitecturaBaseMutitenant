using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using static ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser.AssistantRealProviderFixture;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

internal static class AssistantRealProviderScenario
{
    private const string Root = "/api/organization/assistant";
    internal sealed record Completed(Guid ConversationId, Guid OperationId, string Text, bool ToolCompleted);

    internal static async Task<Completed> CompleteAsync(HttpClient client, Guid ownerId, string prompt, bool attachments, bool stop, CancellationToken ct)
    {
        using var created = await CreateAsync(client, ct);
        var id = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("version").GetUInt32();
        var attachmentIds = new List<Guid>();
        if (attachments)
        {
            foreach (var file in AssistantRealProviderAssets.Files())
            {
                using var multipart = new MultipartFormDataContent();
                var bytes = new ByteArrayContent(file.Bytes);
                bytes.Headers.ContentType = new MediaTypeHeaderValue(file.Type);
                multipart.Add(bytes, "file", file.Name);
                multipart.Add(new StringContent(System.Xml.XmlConvert.ToString(version)), "conversationVersion");
                using var upload = Request(HttpMethod.Post, Root + $"/conversations/{id:D}/attachments", multipart, Guid.NewGuid());
                using var uploaded = await client.SendAsync(upload, ct);
                Require(uploaded.StatusCode == HttpStatusCode.Created, "AttachmentUpload");
                using var body = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync(ct));
                attachmentIds.Add(body.RootElement.GetProperty("id").GetGuid());
                using var current = await ReadAsync(client, Root + $"/conversations/{id:D}", ct);
                version = current.RootElement.GetProperty("conversation").GetProperty("version").GetUInt32();
            }
        }
        var route = Root + $"/conversations/{id:D}/messages";
        var key = Guid.NewGuid();
        var bodyText = JsonSerializer.Serialize(new { text = prompt, attachmentIds, conversationVersion = version });
        using var send = Request(HttpMethod.Post, route, new StringContent(bodyText, Encoding.UTF8, "application/json"), key);
        using var response = await client.SendAsync(send, HttpCompletionOption.ResponseHeadersRead, ct).WaitAsync(TimeSpan.FromSeconds(45), ct);
        Require(response.StatusCode == HttpStatusCode.OK, "SendHttp");
        using var stream = await AssistantSseTestReader.OpenAsync(response, ct);
        Guid turnId = Guid.Empty;
        var hasDelta = false;
        var stopSent = false;
        var toolComplete = false;
        var title = false;
        AssistantTurnResultResponse? result = null;
        while (true)
        {
            var frame = await stream.NextAsync(ct);
            if (frame.Event == "start") turnId = frame.Data.GetProperty("turnId").GetGuid();
            if (frame.Event == "tool")
            {
                Require(frame.Data.GetProperty("toolKey").GetString() == "org_users", "UnexpectedTool");
                Require(!frame.Data.TryGetProperty("arguments", out _) && !frame.Data.TryGetProperty("result", out _), "ToolContentDisclosure");
                toolComplete |= frame.Data.GetProperty("state").GetString() == "complete";
            }
            if (frame.Event == "title") title = true;
            if (frame.Event == "delta")
            {
                hasDelta = true;
                if (stop && !stopSent)
                {
                    Require(turnId != Guid.Empty, "StartBeforeDelta");
                    using var stopRequest = Request(HttpMethod.Post, Root + $"/conversations/{id:D}/turns/{turnId:D}/stop", new StringContent("{}", Encoding.UTF8, "application/json"), Guid.NewGuid());
                    using var stopped = await client.SendAsync(stopRequest, ct);
                    Require(stopped.StatusCode == HttpStatusCode.NoContent, "StopHttp");
                    stopSent = true;
                }
            }
            if (frame.Event == "error")
            {
                // Sólo códigos cerrados; nunca cuerpos del proveedor ni texto de la respuesta.
                var code = frame.Data.TryGetProperty("code", out var field) ? field.GetString() : null;
                Require(false, code is not null && code.StartsWith("Assistant.", StringComparison.Ordinal) ? code : "ProviderStreamError");
            }
            if (frame.Event == "done") { result = frame.Data.Deserialize<AssistantTurnResultResponse>(JsonOptions); break; }
        }
        Require(result is not null && hasDelta, "DurableResponse");
        var completed = result ?? throw new ProbeFailure("DurableResponse");
        Require(completed.Message.Status == (stop ? AssistantMessageStatus.Stopped : AssistantMessageStatus.Complete), "TerminalStatus");
        Require(stop ? stopSent && !title : title, "TitleAndStop");
        using var replayRequest = Request(HttpMethod.Post, route, new StringContent(bodyText, Encoding.UTF8, "application/json"), key);
        using var replay = await client.SendAsync(replayRequest, ct);
        Require(replay.StatusCode == HttpStatusCode.OK && replay.Headers.Contains("Idempotent-Replayed"), "TerminalReplay");
        using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(ct));
        Require(replayBody.RootElement.GetProperty("turnId").GetGuid() == completed.TurnId, "ReplayIdentity");
        Report(new { stage = stop ? "stop" : attachments ? "tool-image-pdf" : "own-key", status = completed.Message.Status.ToString(), toolCompleted = toolComplete, title, replay = true });
        return new(id, completed.TurnId, completed.Message.Text, toolComplete);
    }
    internal static async Task AssertBudgetBlockedAsync(HttpClient client, CancellationToken ct)
    {
        using var conversation = await CreateAsync(client, ct);
        var id = conversation.RootElement.GetProperty("id").GetGuid();
        using var request = Request(HttpMethod.Post, Root + $"/conversations/{id:D}/messages",
            new StringContent(JsonSerializer.Serialize(new { text = "Respuesta breve", attachmentIds = Array.Empty<Guid>(), conversationVersion = conversation.RootElement.GetProperty("version").GetUInt32() }), Encoding.UTF8, "application/json"), Guid.NewGuid());
        using var response = await client.SendAsync(request, ct);
        Require(response.StatusCode == HttpStatusCode.TooManyRequests, "BudgetBlocksAdmission");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Require(problem.RootElement.GetProperty("code").GetString() == "Assistant.Limit.MonthlyReached", "BudgetProblemCode");
        Report(new { stage = "budget-block", status = (int)response.StatusCode });
    }
    internal static async Task ExportAsync(HttpClient client, Guid conversationId, string directory, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        foreach (var (format, extension, contentType) in new[] { ("markdown", "md", "text/markdown"), ("pdf", "pdf", "application/pdf") })
        {
            using var response = await client.GetAsync(Root + $"/conversations/{conversationId:D}/export?format={format}", ct);
            Require(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "PrivateExport");
            Require(response.Content.Headers.ContentType?.MediaType == contentType, "ExportType");
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            Require(bytes.Length > 0 && (format != "pdf" || Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-"), "ExportBinary");
            await File.WriteAllBytesAsync(Path.Combine(directory, "anthropic-real." + extension), bytes, ct);
            Report(new { stage = "export", format, status = (int)response.StatusCode, bytes = bytes.Length });
        }
    }
    private static async Task<JsonDocument> CreateAsync(HttpClient client, CancellationToken ct)
    {
        using var create = Request(HttpMethod.Post, Root + "/conversations", new StringContent("{}", Encoding.UTF8, "application/json"), Guid.NewGuid());
        using var response = await client.SendAsync(create, ct);
        Require(response.StatusCode == HttpStatusCode.Created, "ConversationCreate");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    private static HttpRequestMessage Request(HttpMethod method, string path, HttpContent? content, Guid key)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return request;
    }
}
