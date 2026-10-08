using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>Un único POST por intento durable; conserva sólo códigos estáticos y el identificador aceptado.</summary>
internal sealed class WhatsAppCloudClient(HttpClient client, IOptions<WhatsAppOptions> options)
{
    public async Task<OutboxDeliveryReceipt> SendAsync(WhatsAppOutboundMessage message, CancellationToken ct)
    {
        message.Validate();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            options.Value.GraphApiVersion + "/" + Uri.EscapeDataString(message.PhoneNumberId) + "/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);
        request.Content = JsonContent.Create(BuildPayload(message, options.Value.SendArgentineMobilesWithoutNine));
        try
        {
            using var response = await client.SendAsync(request, ct);
            JsonDocument parsed;
            try { parsed = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct); }
            catch (JsonException)
            {
                return response.IsSuccessStatusCode
                    ? OutboxDeliveryReceipt.Accepted(providerScope: message.PhoneNumberId, category: message.Kind.ToString())
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
                return OutboxDeliveryReceipt.Accepted(wamid, message.PhoneNumberId, message.Kind.ToString());
            }
            var code = 0;
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.Number)
                value.TryGetInt32(out code);
            var safeCode = code switch
            {
                131030 => "WhatsApp.Send.RecipientNotAllowed", 131047 => "WhatsApp.Send.WindowExpired",
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
        var components = new List<object>();
        if (message.BodyParameters.Count > 0)
            components.Add(new { type = "body", parameters = message.BodyParameters.Select(value => new { type = "text", text = value }).ToArray() });
        if (message.ButtonParameter is not null)
            components.Add(new { type = "button", sub_type = "url", index = "0", parameters = new[] { new { type = "text", text = message.ButtonParameter } } });
        return new { messaging_product = "whatsapp", recipient_type = "individual", to, type = "template",
            template = new { name = message.TemplateName, language = new { code = message.Language }, components } };
    }
}
