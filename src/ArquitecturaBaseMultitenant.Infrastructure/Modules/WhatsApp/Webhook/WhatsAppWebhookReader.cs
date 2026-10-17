using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Webhook;
/// <summary>Lee cada número de destino tal como vino; el escritor descarta los que no son un número vigente. Un ítem desconocido no invalida los demás.</summary>
internal sealed class WhatsAppWebhookReader(IPhoneNumberParser phones) : IWhatsAppWebhookReader
{
    public Result<WhatsAppWebhookBatch> Read(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (Text(root, "object") != "whatsapp_business_account" || !Array(root, "entry", out var entries)) return WhatsAppErrors.InvalidPayload;
            var messages = new List<WhatsAppWebhookMessage>(); var statuses = new List<WhatsAppWebhookStatus>(); var templates = new List<WhatsAppWebhookTemplate>();
            foreach (var entry in entries.EnumerateArray())
            {
                if (!Array(entry, "changes", out var changes)) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (Text(change, "field") is "message_template_status_update" or "template_category_update")
                    {
                        if (Template(Text(entry, "id"), change) is { } template) templates.Add(template);
                        continue;
                    }
                    if (Text(change, "field") != "messages" || !Property(change, "value", out var value)
                        || !Property(value, "metadata", out var metadata)
                        || Text(metadata, "phone_number_id") is not { Length: > 0 and <= TextLimits.ShortName } number) continue;
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
                            if (type == "button" && Property(message, "button", out var button)) { reply = Text(button, "payload"); text = Text(button, "text"); }
                            if (type == "interactive" && Property(message, "interactive", out var interactive)
                                && (Property(interactive, "button_reply", out var choice) || Property(interactive, "list_reply", out choice)))
                            {
                                reply = Text(choice, "id");
                                // Lo que eligió queda en la conversación con el texto que vio.
                                text = Text(choice, "title");
                            }
                            // Solo identificadores de respuestas que ofrece el bot; nunca un token recibido.
                            reply = BotButtons.IsKnown(reply) ? reply : null;
                            // Lo que no es texto ni respuesta se informa por su tipo, sin el archivo (D7 de la E11).
                            var contentType = kind == WhatsAppMessageKind.Unsupported ? Short(type) : null;
                            messages.Add(new(number, id, sender.Value.Value, kind, text, reply, time, contentType));
                        }
                    if (Array(value, "statuses", out var delivery))
                        foreach (var status in delivery.EnumerateArray())
                        {
                            WhatsAppMessageStatus? state = Text(status, "status") switch
                            { "sent" => WhatsAppMessageStatus.Sent, "delivered" => WhatsAppMessageStatus.Delivered, "read" => WhatsAppMessageStatus.Read, "failed" => WhatsAppMessageStatus.Failed, _ => null };
                            if (state is null || Id(status) is not { } id || !Timestamp(status, out var time)) continue;
                            string? category = null, pricingType = null; int? error = null;
                            if (Property(status, "pricing", out var pricing)) { category = Short(Text(pricing, "category")); pricingType = Short(Text(pricing, "type")); }
                            if (Array(status, "errors", out var errors) && errors.GetArrayLength() > 0 && Property(errors[0], "code", out var code)
                                && code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var errorCode)) error = errorCode;
                            statuses.Add(new(number, id, state.Value, time, category, pricingType, error));
                        }
                }
            }
            return new WhatsAppWebhookBatch(messages, statuses, templates);
        }
        catch (JsonException) { return WhatsAppErrors.InvalidPayload; }
    }
    /// <summary>
    /// El estado (message_template_status_update) o la categoría (template_category_update) de una plantilla de la cuenta
    /// de la entrada. El id de Meta llega como número; el motivo, solo como código.
    /// </summary>
    private static WhatsAppWebhookTemplate? Template(string? wabaId, JsonElement change)
    {
        if (wabaId is not { Length: > 0 and <= WhatsAppTextLimits.MetaIdentifier } || !Property(change, "value", out var value)
            || Text(value, "message_template_name") is not { Length: > 0 and <= TextLimits.ShortName } name
            || !name.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_')
            || Text(value, "message_template_language") is not { Length: > 0 and <= TextLimits.ShortCode } language
            || !language.All(character => char.IsAsciiLetter(character) || character is '_' or '-')) return null;
        string? id = null;
        if (Property(value, "message_template_id", out var raw))
            id = raw.ValueKind == JsonValueKind.Number ? raw.GetRawText() : raw.ValueKind == JsonValueKind.String ? raw.GetString() : null;
        if (id is not null && (id.Length > WhatsAppTextLimits.MetaIdentifier || !id.All(char.IsAsciiDigit))) id = null;
        if (Text(change, "field") == "template_category_update")
            return new(wabaId, new WhatsAppMetaTemplate(id, name, language, null, WhatsAppMetaTemplateValues.Category(Text(value, "new_category")), null));
        var status = Text(value, "event");
        return new(wabaId, new WhatsAppMetaTemplate(id, name, language, WhatsAppMetaTemplateValues.Status(status),
            WhatsAppMetaTemplateValues.Category(Text(value, "message_template_category")), WhatsAppMetaTemplateValues.Reason(Text(value, "reason")),
            WhatsAppMetaTemplateValues.IsDeleted(status)));
    }
    /// <summary>Solo claves técnicas cortas de Meta (por ejemplo «service», «regular»), nunca texto libre.</summary>
    private static string? Short(string? value) => value is { Length: > 0 and <= 40 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? value : null;
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
