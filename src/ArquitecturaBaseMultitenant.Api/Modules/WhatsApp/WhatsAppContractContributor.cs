using System.Globalization;
using System.Resources;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp;

/// <summary>El módulo aporta sus ejemplos, países y errores; el núcleo no conoce su clave.</summary>
internal sealed class WhatsAppContractContributor : IHttpContractContributor
{
    private static readonly string[] CodePaths = ["/api/auth/login-code", "/api/auth/signup", "/api/auth/business-signup/code", "/api/me/login-methods", "/api/me/login-methods/{methodId}/code", "/api/account-recovery/code"];
    private static readonly string[] NewDestinationPaths = ["/api/auth/signup", "/api/auth/signup/verify", "/api/auth/business-signup/code", "/api/auth/business-signup/verify", "/api/me/login-methods", "/api/account-recovery/code", "/api/account-recovery/verify", "/api/account-recovery"];

    public void Describe(OpenApiDocument document)
    {
        foreach (var path in CodePaths)
            HttpContractDocumentation.Describe(document, path, HttpMethod.Post,
                "WhatsApp: cuota diaria persistente de códigos por UTC, una reserva por operación; replay no vuelve a gastar cuota. Error de entrega posterior a 202 se maneja en outbox.",
                [E(429, "WhatsApp.LoginCode.DailyLimit")]);
        foreach (var path in NewDestinationPaths)
            HttpContractDocumentation.Describe(document, path, HttpMethod.Post,
                "País habilitado se valida solo para destino nuevo; cambiar AllowedCountries no invalida un Phone ya verificado.",
                [E(400, "WhatsApp.Phone.CountryNotAllowed", "phone")]);
        foreach (var item in document.Paths.Values)
            foreach (var operation in item.Operations?.Values.AsEnumerable() ?? [])
                if (operation.RequestBody?.Content?.TryGetValue("application/json", out var media) == true)
                    foreach (var example in media.Examples?.Values ?? [])
                        if (example.Value is JsonObject value && value["channel"]?.GetValue<string>() == "<phone-channel-key>")
                            value["channel"] = "whatsapp";
        if (document.Paths.TryGetValue("/api/auth/methods", out var methods) && methods.Operations?.TryGetValue(HttpMethod.Get, out var listing) == true
            && listing.Responses?.TryGetValue("200", out var response) == true && response.Content?.TryGetValue("application/json", out var content) == true)
            content.Examples = new Dictionary<string, IOpenApiExample>
            {
                ["moduloDisponible"] = new OpenApiExample { Value = JsonNode.Parse("{\"channels\":[{\"key\":\"email\",\"countries\":[]},{\"key\":\"google\",\"countries\":[]},{\"key\":\"whatsapp\",\"countries\":[\"AR\"],\"contactPhone\":\"+5491123456789\"}]}") },
                ["moduloApagado"] = new OpenApiExample { Value = JsonNode.Parse("{\"channels\":[{\"key\":\"email\",\"countries\":[]}]}") },
            };
        HttpContractDocumentation.Describe(document, "/webhooks/whatsapp", HttpMethod.Get,
            "Ruta técnica global condicional. hub.mode=subscribe y VerifyToken correcto: challenge text/plain. Sin configuración/disponibilidad o módulo retirado: 404 común. T01 apagado siempre; T02 aporta disponibilidad y T05 verificación, sin sesión.",
            [E(401, "WhatsApp.Webhook.InvalidSignature"), HttpContractDocumentation.CoreError(404, "Http.NotFound"), HttpContractDocumentation.CoreError(501, "General.Contract.NotImplemented")]);
        HttpContractDocumentation.Describe(document, "/webhooks/whatsapp", HttpMethod.Post,
            "Meta JSON; X-Hub-Signature-256=sha256=<HMAC> sobre bytes originales, antes de confiar en el JSON. Máximo 5242880 bytes, rate limit del webhook. Sin Idempotency-Key: deduplicar IDs Meta por tipo de evento, responder duplicados 200. 200 solo después de persistir lote; worker durable, sin sesión. T01 apagado siempre; stub 501 si se habilita en prueba.",
            [E(400, "WhatsApp.Webhook.InvalidPayload"), E(401, "WhatsApp.Webhook.InvalidSignature"), E(413, "WhatsApp.Webhook.PayloadTooLarge"), E(429, "WhatsApp.Webhook.TooManyRequests"), HttpContractDocumentation.CoreError(404, "Http.NotFound"), HttpContractDocumentation.CoreError(501, "General.Contract.NotImplemented")],
            examples: new Dictionary<string, JsonNode>
            {
                ["mensajeEntrante"] = JsonNode.Parse("{\"object\":\"whatsapp_business_account\",\"entry\":[{\"id\":\"example-waba\",\"changes\":[{\"field\":\"messages\",\"value\":{\"messaging_product\":\"whatsapp\",\"metadata\":{\"display_phone_number\":\"5491123456789\",\"phone_number_id\":\"example-platform-number-id\"},\"messages\":[{\"from\":\"5491198765432\",\"id\":\"wamid.example\",\"timestamp\":\"1791457200\",\"type\":\"text\",\"text\":{\"body\":\"Hola\"}}]}}]}]}")!,
            });
    }

    private static HttpContractError E(int status, string code, string? field = null)
    {
        var resources = new ResourceManager("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources.WhatsAppTexts", typeof(WhatsAppWebhookRequest).Assembly);
        return new HttpContractError(status, code, field,
            resources.GetString(code, CultureInfo.GetCultureInfo("es")) ?? code,
            resources.GetString(code, CultureInfo.GetCultureInfo("en")) ?? code);
    }
}
