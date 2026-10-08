using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;

/// <summary>Envelope Meta. Entry conserva su estructura externa; T05 procesa firma sobre los bytes originales.</summary>
public sealed record WhatsAppWebhookHttpRequest([property: JsonPropertyName("object")] string? ObjectKind, IReadOnlyList<JsonElement>? Entry)
{
    public override string ToString() => nameof(WhatsAppWebhookHttpRequest);
}
