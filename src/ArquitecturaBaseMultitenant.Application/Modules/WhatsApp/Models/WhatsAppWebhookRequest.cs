using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>OriginalBody es la fuente de HMAC; el envelope no se usa como prueba ni se reserializa para validar firma.</summary>
public sealed record WhatsAppWebhookRequest(string? ObjectKind, IReadOnlyList<JsonElement>? Entry, string? Signature,
    ReadOnlyMemory<byte> OriginalBody = default)
{
    public override string ToString() => nameof(WhatsAppWebhookRequest);
}
