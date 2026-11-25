using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// Un único POST por intento durable; conserva sólo el código de Meta y el identificador aceptado. Clasifica cada falla sin leer
/// más que ese código (ADR 0066):
/// <list type="bullet">
/// <item>no salió (sin conexión, un 429, un código pasajero o una falla antes del POST, al buscar el número o abrir su token):
/// vuelve a la cola, que lo reintenta a los 30 s, dos veces como mucho (<see cref="MaxAttempts"/>);</item>
/// <item>lo definitivo falla con uno de los motivos de <see cref="WhatsAppSendFailures"/>;</item>
/// <item>no se sabe (la espera vencida, la respuesta cortada, un 5xx sin código o una respuesta ilegible): «Sin confirmar»,
/// que no se repite.</item>
/// </list>
/// </summary>
internal sealed class WhatsAppCloudClient(HttpClient client, IOptions<WhatsAppOptions> options, TimeProvider clock,
    WhatsAppChannelCredentials? credentials = null, ILogger<WhatsAppCloudClient>? logger = null)
{
    /// <summary>El primer envío y dos reintentos: el tope de intentos del canal en la cola.</summary>
    internal const int MaxAttempts = 3;

    /// <summary>El código técnico de lo que quedó «Sin confirmar»: no es un motivo de «No se pudo enviar».</summary>
    internal const string UncertainCode = "WhatsApp.Send.Uncertain";

    public async Task<OutboxDeliveryReceipt> SendAsync(WhatsAppOutboundMessage message, CancellationToken ct)
    {
        // Todo lo de antes del POST lo resuelve el cliente: una excepción que sale de acá la cola la toma como «Sin confirmar».
        // Un mensaje que no se puede armar no sale nunca: falla sin llamar a Meta, como el payload ilegible del correo.
        try { message.Validate(); }
        catch (ArgumentException) { return Definitive(WhatsAppSendFailures.Rejected); }
        if (message.Body is not null && message.ReplyExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            return OutboxDeliveryReceipt.Rejected(WhatsAppSendFailures.WindowClosed, retryable: false);
        WhatsAppChannelCredential? channel;
        try
        {
            // Desde la E11 el número y el token salen del canal; sin credenciales (pruebas del cliente) vale la configuración.
            channel = credentials is null ? new WhatsAppChannelCredential(message.PhoneNumberId, options.Value.AccessToken)
                : await credentials.FindAsync(message.ChannelId, message.PhoneNumberId, ct);
        }
        catch (Exception exception)
        {
            // La base que no contesta, un token que este anillo de claves no abre o la espera vencida mientras se lee el canal:
            // Meta no recibió nada. Vuelve a la cola, que puede tomarlo otra copia.
            if (logger is not null) ExceptionLog.Write(logger, nameof(WhatsAppCloudClient), exception);
            return Retry(WhatsAppSendFailures.Unavailable);
        }
        if (channel is null) return OutboxDeliveryReceipt.Rejected(WhatsAppSendFailures.ReadOnly, retryable: false);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            options.Value.GraphApiVersion + "/" + Uri.EscapeDataString(channel.PhoneNumberId) + "/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", channel.Token);
        request.Content = JsonContent.Create(BuildPayload(message, options.Value.SendArgentineMobilesWithoutNine));
        try
        {
            using var response = await client.SendAsync(request, ct);
            var body = await ReadJsonAsync(response, ct);
            if (response.IsSuccessStatusCode)
                // Sin el id, Meta pudo haberlo mandado igual: no se sabe, y no se repite.
                return MessageIdOf(body) is { } wamid
                    ? OutboxDeliveryReceipt.Accepted(wamid.Length > TextLimits.Description ? null : wamid, channel.PhoneNumberId, message.Kind.ToString())
                    : OutboxDeliveryReceipt.Uncertain(UncertainCode);
            var code = ErrorCodeOf(body);
            // Un 5xx sin código de Meta (un proxy, una caída a mitad de camino) no dice si lo procesó.
            if (code is null && (int)response.StatusCode >= 500) return OutboxDeliveryReceipt.Uncertain(UncertainCode);
            return Classify(response.StatusCode, code);
        }
        catch (HttpRequestException exception) when (exception.HttpRequestError is HttpRequestError.NameResolutionError
            or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError)
        {
            // El DNS, la conexión o el TLS no llegaron a Meta: no salió, y se reintenta.
            return OutboxDeliveryReceipt.Rejected(WhatsAppSendFailures.Unavailable, retryable: true);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException
            or TimeoutRejectedException)
        {
            // La respuesta cortada o la espera vencida (la de la cola, la del cliente o la de la resiliencia): Meta pudo haberlo recibido.
            return OutboxDeliveryReceipt.Uncertain(UncertainCode);
        }
        catch (ExecutionRejectedException)
        {
            // El circuito abierto o el límite de la resiliencia cortaron antes de mandar: no salió.
            return OutboxDeliveryReceipt.Rejected(WhatsAppSendFailures.Unavailable, retryable: true);
        }
    }

    /// <summary>
    /// Manda el código de Meta, que gana sobre el estado: el 3, por ejemplo, llega con un 500 y no es pasajero. Los de límite
    /// («Throttling errors» de la Cloud API: 4, 80007, 130429, 131048 y 131056) llegan con un 400 y se reintentan. De un 5xx solo
    /// se reintentan los que Meta pide reintentar (131000, 131016, 2 y 133004). Sin un código conocido decide el estado: un 401
    /// es el token, un 403 un permiso y un 429 un límite, que se reintenta.
    /// </summary>
    internal static OutboxDeliveryReceipt Classify(HttpStatusCode status, int? code) => code switch
    {
        131030 => Definitive(WhatsAppSendFailures.NotAllowed),
        131047 => Definitive(WhatsAppSendFailures.WindowClosed),
        131026 => Definitive(WhatsAppSendFailures.Undeliverable),
        131042 => Definitive(WhatsAppSendFailures.PaymentRequired),
        4 or 80007 or 130429 or 131048 or 131056 => Retry(WhatsAppSendFailures.RateLimited),
        131000 or 131016 or 2 or 133004 when (int)status >= 500 => Retry(WhatsAppSendFailures.Unavailable),
        0 or 190 => Definitive(WhatsAppSendFailures.TokenInvalid),
        3 or 10 or (>= 200 and <= 299) => Definitive(WhatsAppSendFailures.PermissionMissing),
        _ when status == HttpStatusCode.Unauthorized => Definitive(WhatsAppSendFailures.TokenInvalid),
        _ when status == HttpStatusCode.Forbidden => Definitive(WhatsAppSendFailures.PermissionMissing),
        _ when status == HttpStatusCode.TooManyRequests => Retry(WhatsAppSendFailures.RateLimited),
        _ => Definitive(WhatsAppSendFailures.Rejected),
    };

    private static OutboxDeliveryReceipt Definitive(string code) => OutboxDeliveryReceipt.Rejected(code, retryable: false);

    private static OutboxDeliveryReceipt Retry(string code) => OutboxDeliveryReceipt.Rejected(code, retryable: true);

    /// <summary>Un cuerpo que no es JSON (la página de error de un proxy) no es una excepción: decide el estado.</summary>
    private static async Task<JsonElement?> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return document.RootElement.Clone();
        }
        catch (JsonException) { return null; }
    }

    // { "messages": [ { "id": "wamid...." } ] }. Un id que no entra en la columna sale igual, sin poder seguirlo.
    private static string? MessageIdOf(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("messages", out var messages)
        && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0 && messages[0].ValueKind == JsonValueKind.Object
        && messages[0].TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
        && id.GetString() is { } wamid && !string.IsNullOrWhiteSpace(wamid) ? wamid : null;

    // { "error": { "code": 131047, ... } }. El mensaje y los detalles de Meta no se leen: pueden repetir lo que se mandó.
    private static int? ErrorCodeOf(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("error", out var error)
        && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var code) ? code : null;

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
                ? new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = "cta_url",
                    ["parameters"] = new System.Text.Json.Nodes.JsonObject
                    { ["display_text"] = message.LinkLabel, ["url"] = message.LinkUrl }
                }
                : message.ListRows is { } rows
                ? new System.Text.Json.Nodes.JsonObject
                {
                    ["button"] = message.ListButton,
                    ["sections"] = new System.Text.Json.Nodes.JsonArray(
                    new System.Text.Json.Nodes.JsonObject
                    {
                        ["rows"] = new System.Text.Json.Nodes.JsonArray([.. rows.Select(row =>
                        (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject { ["id"] = row.Id, ["title"] = row.Title })])
                    })
                }
                : new System.Text.Json.Nodes.JsonObject
                {
                    ["buttons"] = new System.Text.Json.Nodes.JsonArray(
                    [.. (message.ReplyButtons ?? []).Select(button => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
                        { ["type"] = "reply", ["reply"] = new System.Text.Json.Nodes.JsonObject { ["id"] = button.Id, ["title"] = button.Title } })])
                };
            return new { messaging_product = "whatsapp", recipient_type = "individual", to, type = "interactive", interactive };
        }
        var components = new List<object>();
        if (message.BodyParameters.Count > 0)
            components.Add(new { type = "body", parameters = message.BodyParameters.Select(value => new { type = "text", text = value }).ToArray() });
        if (message.ButtonParameter is not null)
            components.Add(new { type = "button", sub_type = "url", index = "0", parameters = new[] { new { type = "text", text = message.ButtonParameter } } });
        // «No recibir más» de una Marketing: la respuesta trae este identificador y no el texto del botón.
        if (message.QuickReplyPayload is not null)
            components.Add(new { type = "button", sub_type = "quick_reply", index = "0", parameters = new[] { new { type = "payload", payload = message.QuickReplyPayload } } });
        return new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to,
            type = "template",
            template = new { name = message.TemplateName, language = new { code = message.Language }, components }
        };
    }
}
