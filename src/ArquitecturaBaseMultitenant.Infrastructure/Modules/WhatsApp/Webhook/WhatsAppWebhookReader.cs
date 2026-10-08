using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Webhook;
/// <summary>Solo el destino configurado; un ítem desconocido no invalida los demás.</summary>
internal sealed class WhatsAppWebhookReader(IOptions<WhatsAppOptions> options, IPhoneNumberParser phones) : IWhatsAppWebhookReader
{
    public Result<WhatsAppWebhookBatch> Read(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (Text(root, "object") != "whatsapp_business_account" || !Array(root, "entry", out var entries)) return WhatsAppErrors.InvalidPayload;
            var messages = new List<WhatsAppWebhookMessage>(); var statuses = new List<WhatsAppWebhookStatus>();
            foreach (var entry in entries.EnumerateArray())
            {
                if (!Array(entry, "changes", out var changes)) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (Text(change, "field") != "messages" || !Property(change, "value", out var value)
                        || !Property(value, "metadata", out var metadata)
                        || Text(metadata, "phone_number_id") is not { } number || number != options.Value.PhoneNumberId) continue;
                    if (Array(value, "messages", out var inbound))
                        foreach (var message in inbound.EnumerateArray())
                        {
                            if (Id(message) is not { } id || !Timestamp(message, out var time) || Text(message, "from") is not { } from) continue;
                            var sender = phones.Parse(null, from.StartsWith('+') ? from : "+" + from, PhoneUsage.Mobile);
                            if (sender.IsFailure) continue;
                            var type = Text(message, "type");
                            var kind = type switch { "text" => WhatsAppMessageKind.Text, "interactive" or "button" => WhatsAppMessageKind.Interactive, _ => WhatsAppMessageKind.Unsupported };
                            string? text = null; string? reply = null;
                            if (type == "text" && Property(message, "text", out var content)) text = Text(content, "body");
                            if (type == "button" && Property(message, "button", out var button)) reply = Text(button, "payload");
                            if (type == "interactive" && Property(message, "interactive", out var interactive)
                                && Property(interactive, "button_reply", out var buttonReply)) reply = Text(buttonReply, "id");
                            // Solo identificadores de acciones públicas del bot; nunca un token recibido.
                            reply = reply is BotButtons.SignIn or BotButtons.Recover ? reply : null;
                            messages.Add(new(number, id, sender.Value.Value, kind, text, reply, time));
                        }
                    if (Array(value, "statuses", out var delivery))
                        foreach (var status in delivery.EnumerateArray())
                        {
                            WhatsAppMessageStatus? state = Text(status, "status") switch
                            { "sent" => WhatsAppMessageStatus.Sent, "delivered" => WhatsAppMessageStatus.Delivered, "read" => WhatsAppMessageStatus.Read, "failed" => WhatsAppMessageStatus.Failed, _ => null };
                            if (state is not null && Id(status) is { } id && Timestamp(status, out var time)) statuses.Add(new(number, id, state.Value, time));
                        }
                }
            }
            return new WhatsAppWebhookBatch(messages, statuses);
        }
        catch (JsonException) { return WhatsAppErrors.InvalidPayload; }
    }
    private static string? Id(JsonElement element)
    {
        var id = Text(element, "id");
        return id is { Length: > 0 and <= TextLimits.Description } && !id.Any(char.IsWhiteSpace) ? id : null;
    }
    private static bool Timestamp(JsonElement element, out DateTime time)
    {
        time = default;
        if (!long.TryParse(Text(element, "timestamp"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) return false;
        time = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        return true;
    }
    private static bool Array(JsonElement element, string name, out JsonElement value) =>
        Property(element, name, out value) && value.ValueKind == JsonValueKind.Array;
    private static string? Text(JsonElement element, string name)
    {
        if (!Property(element, name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        try { return value.GetString(); } catch (InvalidOperationException) { return null; }
    }
    private static bool Property(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        try { return element.TryGetProperty(name, out value); }
        catch (InvalidOperationException)
        {
            var found = false;
            foreach (var property in element.EnumerateObject())
                try { if (property.NameEquals(name)) { value = property.Value; found = true; } } catch (InvalidOperationException) { }
            return found;
        }
    }
}
