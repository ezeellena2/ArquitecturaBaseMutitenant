using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// Las plantillas de una cuenta de WhatsApp en la Graph API (<c>/&lt;WABA_ID&gt;/message_templates</c>): crear con datos
/// posicionales y sus ejemplos, borrar por nombre y listar con su estado. De Meta se conserva solo el código del error.
/// </summary>
internal sealed class WhatsAppTemplateClient(HttpClient client, IOptions<WhatsAppOptions> options) : IWhatsAppTemplateClient
{
    /// <summary>Meta ya tiene ese nombre en ese idioma (código 100, subcódigo 2388024).</summary>
    private const int AlreadyExists = 2388024;
    private const int MaxPages = 50;

    public async Task<Result<WhatsAppMetaTemplate>> CreateAsync(string wabaId, string token, WhatsAppTemplateSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var body = new JsonObject { ["type"] = "BODY", ["text"] = submission.Body };
        if (submission.Examples.Count > 0)
            body["example"] = new JsonObject { ["body_text"] = new JsonArray(new JsonArray([.. submission.Examples.Select(value => (JsonNode)JsonValue.Create(value))])) };
        var components = new JsonArray(body);
        if (submission.UnsubscribeButton is { } unsubscribe)
            components.Add(new JsonObject { ["type"] = "BUTTONS", ["buttons"] = new JsonArray(new JsonObject { ["type"] = "QUICK_REPLY", ["text"] = unsubscribe }) });
        var payload = new JsonObject
        {
            ["name"] = submission.MetaName, ["language"] = submission.MetaLanguage, ["category"] = submission.Category.ToString().ToUpperInvariant(),
            ["parameter_format"] = "positional", ["components"] = components,
        };
        var created = await SendAsync(HttpMethod.Post, Uri.EscapeDataString(wabaId) + "/message_templates", token, payload, ct);
        if (created.IsFailure) return created.Error;
        using var document = created.Value;
        var root = document.RootElement;
        return new WhatsAppMetaTemplate(Identifier(root), submission.MetaName, submission.MetaLanguage,
            WhatsAppMetaTemplateValues.Status(Text(root, "status")) ?? WhatsAppTemplateStatus.Pending,
            WhatsAppMetaTemplateValues.Category(Text(root, "category")) ?? submission.Category, null);
    }

    public async Task<Result> DeleteAsync(string wabaId, string token, string metaName, CancellationToken ct)
    {
        var deleted = await SendAsync(HttpMethod.Delete, Uri.EscapeDataString(wabaId) + "/message_templates?name=" + Uri.EscapeDataString(metaName), token, null, ct);
        if (deleted.IsFailure) return deleted.Error;
        deleted.Value.Dispose();
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<WhatsAppMetaTemplate>>> ListAsync(string wabaId, string token, CancellationToken ct)
    {
        var templates = new List<WhatsAppMetaTemplate>();
        string? after = null;
        for (var page = 0; page < MaxPages; page++)
        {
            var listed = await SendAsync(HttpMethod.Get, Uri.EscapeDataString(wabaId) + "/message_templates?fields=id,name,language,status,category,rejected_reason&limit=100"
                + (after is null ? string.Empty : "&after=" + Uri.EscapeDataString(after)), token, null, ct);
            if (listed.IsFailure) return listed.Error;
            using var document = listed.Value;
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                foreach (var item in data.EnumerateArray())
                    if (Text(item, "name") is { Length: > 0 and <= TextLimits.ShortName } name && Text(item, "language") is { Length: > 0 and <= TextLimits.ShortCode } language)
                        templates.Add(new WhatsAppMetaTemplate(Identifier(item), name, language, WhatsAppMetaTemplateValues.Status(Text(item, "status")),
                            WhatsAppMetaTemplateValues.Category(Text(item, "category")), WhatsAppMetaTemplateValues.Reason(Text(item, "rejected_reason"))));
            after = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("paging", out var paging) && paging.ValueKind == JsonValueKind.Object
                && paging.TryGetProperty("next", out _) && paging.TryGetProperty("cursors", out var cursors) ? Text(cursors, "after") : null;
            if (after is null) return templates;
        }
        return templates;
    }

    private async Task<Result<JsonDocument>> SendAsync(HttpMethod method, string path, string token, JsonObject? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, options.Value.GraphApiVersion + "/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await client.SendAsync(request, ct);
            JsonDocument document;
            try { document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct); }
            catch (JsonException) { return WhatsAppErrors.NumberMetaRejected("invalid_response"); }
            if (response.IsSuccessStatusCode) return document;
            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                    return WhatsAppErrors.NumberMetaRejected("http");
                if (error.TryGetProperty("error_subcode", out var subcode) && subcode.TryGetInt32(out var sub) && sub == AlreadyExists)
                    return WhatsAppErrors.TemplateNameTaken;
                return WhatsAppErrors.NumberMetaRejected(error.TryGetProperty("code", out var code) && code.TryGetInt32(out _) ? code.GetRawText() : "http");
            }
        }
        catch (HttpRequestException) { return WhatsAppErrors.NumberMetaRejected("network"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return WhatsAppErrors.NumberMetaRejected("timeout"); }
    }

    private static string? Identifier(JsonElement element) =>
        Text(element, "id") is { Length: > 0 and <= WhatsAppTextLimits.MetaIdentifier } id && id.All(char.IsAsciiDigit) ? id : null;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
