using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;

public sealed record WhatsAppWebhookQuery
{
    [FromQuery(Name = "hub.mode")] public string? Mode { get; init; }
    [FromQuery(Name = "hub.verify_token")] public string? VerifyToken { get; init; }
    [FromQuery(Name = "hub.challenge")] public string? Challenge { get; init; }
    public override string ToString() => nameof(WhatsAppWebhookQuery);
}
