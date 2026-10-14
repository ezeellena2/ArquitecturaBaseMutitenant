using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>Un único POST por intento durable; conserva sólo códigos estáticos y el identificador aceptado.</summary>
internal sealed class WhatsAppCloudClient(HttpClient client, IOptions<WhatsAppOptions> options, TimeProvider clock,
    WhatsAppChannelCredentials? credentials = null)
{
    public async Task<OutboxDeliveryReceipt> SendAsync(WhatsAppOutboundMessage message, CancellationToken ct)
    {
        message.Validate();
        if (message.Body is not null && message.ReplyExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            return OutboxDeliveryReceipt.Rejected("WhatsApp.Send.WindowExpired", retryable: false);
        // Desde la E11 el número y el token salen del canal; sin credenciales (pruebas del cliente) vale la configuración.
        var channel = credentials is null ? new WhatsAppChannelCredential(message.PhoneNumberId, options.Value.AccessToken)
            : await credentials.FindAsync(message.ChannelId, message.PhoneNumberId, ct);
        if (channel is null) return OutboxDeliveryReceipt.Rejected("WhatsApp.Send.ChannelUnavailable", retryable: false);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            options.Value.GraphApiVersion + "/" + Uri.EscapeDataString(channel.PhoneNumberId) + "/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", channel.Token);
        request.Content = JsonContent.Create(BuildPayload(message, options.Value.SendArgentineMobilesWithoutNine));
        try
        {
            using var response = await client.SendAsync(request, ct);
            JsonDocument parsed;
            try { parsed = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct); }
            catch (JsonException)
            {
                return response.IsSuccessStatusCode
                    ? OutboxDeliveryReceipt.Accepted(providerScope: channel.PhoneNumberId, category: message.Kind.ToString())
                    : OutboxDeliveryReceipt.Rejected("WhatsApp.Send.InvalidResponse", response.StatusCode is HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500);
            }
            using var document = parsed;
            if (response.IsSuccessStatusCode)
            {
                string? wamid = null;
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array
                    && messages.GetArrayLength() > 0 && messages[0].ValueKind == JsonValueKind.Object && messages[0].TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    wamid = id.GetString();
                if (wamid?.Length > ArquitecturaBaseMultitenant.Domain.Common.TextLimits.Description) wamid = null;
                return OutboxDeliveryReceipt.Accepted(wamid, channel.PhoneNumberId, message.Kind.ToString());
            }
            var code = 0;
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.Number)
                value.TryGetInt32(out code);
            var safeCode = code switch
            {
                131030 => "WhatsApp.Send.RecipientNotAllowed", 131047 => "WhatsApp.Send.WindowExpired",
                131042 => "WhatsApp.Send.PaymentRequired",
                131026 => "WhatsApp.Send.Undeliverable", 131056 or 130429 => "WhatsApp.Send.RateLimited",
                190 => "WhatsApp.Send.Credentials", 3 or 10 or >= 200 and <= 299 => "WhatsApp.Send.Permissions",
                _ => "WhatsApp.Send.Rejected",
            };
            return OutboxDeliveryReceipt.Rejected(safeCode, code is 131056 or 130429 || response.StatusCode is HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500);
        }
        catch (HttpRequestException) { return OutboxDeliveryReceipt.Rejected("WhatsApp.Send.Network", true); }
        catch (OperationCanceledException) { return OutboxDeliveryReceipt.Rejected("WhatsApp.Send.Timeout", true); }
        catch (JsonException) { return OutboxDeliveryReceipt.Rejected("WhatsApp.Send.InvalidResponse", true); }
    }
    internal static object BuildPayload(WhatsAppOutboundMessage message, bool argentinaWithoutNine = false)
    {
        message.Validate();
        var to = message.To.TrimStart('+');
        if (argentinaWithoutNine && to.StartsWith("549", StringComparison.Ordinal)) to = "54" + to[3..];
        if (message.Body is not null)
        {
            if (message.Kind == ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppMessageKind.Text)
                return new { messaging_product = "whatsapp", recipient_type = "individual", to, type = "text", text = new { body = message.Body } };
            var interactive = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = message.LinkUrl is not null ? "cta_url" : message.ListRows is not null ? "list" : "button",
                ["body"] = new System.Text.Json.Nodes.JsonObject { ["text"] = message.Body },
            };
            if (message.Footer is not null) interactive["footer"] = new System.Text.Json.Nodes.JsonObject { ["text"] = message.Footer };
            interactive["action"] = message.LinkUrl is not null
                ? new System.Text.Json.Nodes.JsonObject { ["name"] = "cta_url", ["parameters"] = new System.Text.Json.Nodes.JsonObject
                    { ["display_text"] = message.LinkLabel, ["url"] = message.LinkUrl } }
                : message.ListRows is { } rows
                ? new System.Text.Json.Nodes.JsonObject { ["button"] = message.ListButton, ["sections"] = new System.Text.Json.Nodes.JsonArray(
                    new System.Text.Json.Nodes.JsonObject { ["rows"] = new System.Text.Json.Nodes.JsonArray([.. rows.Select(row =>
                        (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject { ["id"] = row.Id, ["title"] = row.Title })]) }) }
                : new System.Text.Json.Nodes.JsonObject { ["buttons"] = new System.Text.Json.Nodes.JsonArray(
                    [.. (message.ReplyButtons ?? []).Select(button => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
                        { ["type"] = "reply", ["reply"] = new System.Text.Json.Nodes.JsonObject { ["id"] = button.Id, ["title"] = button.Title } })]) };
            return new { messaging_product = "whatsapp", recipient_type = "individual", to, type = "interactive", interactive };
        }
        var components = new List<object>();
        if (message.BodyParameters.Count > 0)
            components.Add(new { type = "body", parameters = message.BodyParameters.Select(value => new { type = "text", text = value }).ToArray() });
        if (message.ButtonParameter is not null)
            components.Add(new { type = "button", sub_type = "url", index = "0", parameters = new[] { new { type = "text", text = message.ButtonParameter } } });
        return new { messaging_product = "whatsapp", recipient_type = "individual", to, type = "template",
            template = new { name = message.TemplateName, language = new { code = message.Language }, components } };
    }
}
