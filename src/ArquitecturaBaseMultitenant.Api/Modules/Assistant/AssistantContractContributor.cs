using System.Globalization;
using System.Resources;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

/// <summary>Contribuye únicamente a las rutas y esquemas del módulo instalado.</summary>
internal sealed class AssistantContractContributor : IHttpContractContributor
{
    private static readonly ResourceManager Resources = new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources.AssistantTexts", typeof(AssistantStateResponse).Assembly);
    private static string Resource(string code, string culture) => Resources.GetString(code, CultureInfo.GetCultureInfo(culture)) ?? code;
    private static readonly string[] StreamNames = ["Start", "Delta", "Tool", "Title", "Done", "Error"];
    private static readonly HttpContractError[] ModuleErrors =
    [
        new(404, "Assistant.Conversation.NotFound", null, Resource("Assistant.Conversation.NotFound", "es"), Resource("Assistant.Conversation.NotFound", "en")),
        new(404, "Assistant.Attachment.NotFound", null, Resource("Assistant.Attachment.NotFound", "es"), Resource("Assistant.Attachment.NotFound", "en")),
        new(400, "Assistant.Settings.NotConfigured", null, Resource("Assistant.Settings.NotConfigured", "es"), Resource("Assistant.Settings.NotConfigured", "en")),
        new(400, "Assistant.Settings.KeyRejected", "apiKey", Resource("Assistant.Settings.KeyRejected", "es"), Resource("Assistant.Settings.KeyRejected", "en")),
        new(403, "Assistant.Settings.OwnKeyNotAllowed", "mode", Resource("Assistant.Settings.OwnKeyNotAllowed", "es"), Resource("Assistant.Settings.OwnKeyNotAllowed", "en")),
        new(400, "Assistant.Settings.AlertRecipientRequired", "mode", Resource("Assistant.Settings.AlertRecipientRequired", "es"), Resource("Assistant.Settings.AlertRecipientRequired", "en")),
        new(400, "Assistant.Model.NotAllowed", "modelId", Resource("Assistant.Model.NotAllowed", "es"), Resource("Assistant.Model.NotAllowed", "en")),
        new(409, "Assistant.Model.AlreadyExists", "providerModelId", Resource("Assistant.Model.AlreadyExists", "es"), Resource("Assistant.Model.AlreadyExists", "en")),
        new(404, "Assistant.Model.NotFound", null, Resource("Assistant.Model.NotFound", "es"), Resource("Assistant.Model.NotFound", "en")),
        new(400, "Assistant.Settings.PlatformLimitsReadOnly", "dailyMessageLimit", Resource("Assistant.Settings.PlatformLimitsReadOnly", "es"), Resource("Assistant.Settings.PlatformLimitsReadOnly", "en")),
        new(400, "Assistant.Notice.AcceptanceRequired", "noticeVersion", Resource("Assistant.Notice.AcceptanceRequired", "es"), Resource("Assistant.Notice.AcceptanceRequired", "en")),
        new(429, "Assistant.Limit.DailyReached", null, Resource("Assistant.Limit.DailyReached", "es"), Resource("Assistant.Limit.DailyReached", "en")),
        new(429, "Assistant.Limit.MonthlyReached", null, Resource("Assistant.Limit.MonthlyReached", "es"), Resource("Assistant.Limit.MonthlyReached", "en")),
        new(409, "Assistant.Turn.InProgress", null, Resource("Assistant.Turn.InProgress", "es"), Resource("Assistant.Turn.InProgress", "en")),
        new(404, "Assistant.Turn.NotFound", null, Resource("Assistant.Turn.NotFound", "es"), Resource("Assistant.Turn.NotFound", "en")),
        new(400, "Assistant.Attachment.UnsupportedType", "file", Resource("Assistant.Attachment.UnsupportedType", "es"), Resource("Assistant.Attachment.UnsupportedType", "en")),
        new(413, "Assistant.Attachment.TooLarge", "file", Resource("Assistant.Attachment.TooLarge", "es"), Resource("Assistant.Attachment.TooLarge", "en")),
        new(400, "Assistant.Attachment.TooMany", "attachmentIds", Resource("Assistant.Attachment.TooMany", "es"), Resource("Assistant.Attachment.TooMany", "en")),
        new(409, "Assistant.Attachment.AlreadyUsed", "attachmentIds", Resource("Assistant.Attachment.AlreadyUsed", "es"), Resource("Assistant.Attachment.AlreadyUsed", "en")),
        new(400, "Assistant.QuickQuery.NotAvailable", "key", Resource("Assistant.QuickQuery.NotAvailable", "es"), Resource("Assistant.QuickQuery.NotAvailable", "en")),
        new(503, "Assistant.Provider.Unavailable", null, Resource("Assistant.Provider.Unavailable", "es"), Resource("Assistant.Provider.Unavailable", "en")),
        new(400, "Assistant.Provider.ContextTooLarge", "text", Resource("Assistant.Provider.ContextTooLarge", "es"), Resource("Assistant.Provider.ContextTooLarge", "en")),
        new(500, "Assistant.Export.Failed", null, Resource("Assistant.Export.Failed", "es"), Resource("Assistant.Export.Failed", "en")),
    ];
    public void Describe(OpenApiDocument document)
    {
        DescribeSchemas(document);
        foreach (var (path, item) in document.Paths.Where(pair => IsOwnPath(pair.Key)))
            foreach (var (method, operation) in item.Operations ?? [])
            {
                var errors = Errors(path, method);
                var declaredStatuses = errors.Select(error => System.Text.Json.JsonSerializer.Serialize(error.Status)).ToHashSet(StringComparer.Ordinal);
                foreach (var status in operation.Responses?.Keys.ToArray() ?? [])
                    if (int.TryParse(status, out var code) && code >= 400 && !declaredStatuses.Contains(status)) operation.Responses!.Remove(status);
                var idempotent = method == HttpMethod.Post;
                var mode = path.EndsWith("/messages", StringComparison.Ordinal) ? "DurableStream"
                    : path.EndsWith("/conversations", StringComparison.Ordinal) || path.Contains("/quick/", StringComparison.Ordinal)
                        || path.EndsWith("/attachments", StringComparison.Ordinal) ? "DurableReference" : "BufferedJson";
                HttpContractDocumentation.Describe(document, path, method,
                    "T01: contrato publicado, conducta pendiente; el servicio devuelve General.Contract.NotImplemented (501). "
                    + "Identidad, acceso y organización se derivan del contexto firmado. JSON estricto; UTC con Z; versiones uint/xmin. "
                    + Description(path, method), errors, idempotent, authenticated: true,
                    examples: RequestExamples(path, method));
                operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                operation.Extensions["x-access"] = new JsonNodeExtension(JsonValue.Create(path.StartsWith("/api/me/", StringComparison.Ordinal) ? "consumer"
                    : path.StartsWith("/api/organization/", StringComparison.Ordinal) ? "business" : "platform")!);
                operation.Extensions["x-permissions"] = new JsonNodeExtension(Permissions(path, method));
                operation.Extensions["x-module-gate"] = new JsonNodeExtension(JsonValue.Create(path.StartsWith("/api/platform/", StringComparison.Ordinal)
                    ? IsChat(path) ? "installed-and-chat-available" : "installed-management" : "feature:assistant")!);
                if (idempotent) operation.Extensions["x-idempotency-mode"] = new JsonNodeExtension(JsonValue.Create(mode)!);
                foreach (var response in operation.Responses?.Values.AsEnumerable() ?? [])
                    if (response.Content?.ContainsKey("application/json") == true)
                    { response.Content.Remove("text/plain"); response.Content.Remove("text/json"); }
                if (operation.Responses?.TryGetValue("200", out var result) == true
                    || operation.Responses?.TryGetValue("201", out result) == true)
                    AddSuccessExample(result, path, method, document);
                if (path.EndsWith("/messages", StringComparison.Ordinal) && method == HttpMethod.Post) DescribeStream(operation, document);
                DescribeFiles(operation, path, method);
                DescribeQuery(operation, path);
                DescribeHeaders(operation, path);
                if (operation.Responses?.TryGetValue("429", out var limited) == true
                    && limited.Content?.TryGetValue("application/problem+json", out var problem) == true)
                    foreach (var (key, example) in problem.Examples ?? new Dictionary<string, IOpenApiExample>())
                        if (key.StartsWith("Assistant.Limit.DailyReached.", StringComparison.Ordinal) && example.Value is JsonObject body)
                        { body["dailyLimit"] = 50; body["dailyResetAtUtc"] = "2026-10-10T00:00:00Z"; }
            }
    }
    private static bool IsOwnPath(string path) => path.StartsWith("/api/me/assistant", StringComparison.Ordinal)
        || path.StartsWith("/api/organization/assistant", StringComparison.Ordinal)
        || path.StartsWith("/api/platform/assistant", StringComparison.Ordinal)
        || path.StartsWith("/api/platform/tenants/", StringComparison.Ordinal) && path.EndsWith("/assistant/policy", StringComparison.Ordinal);
    private static bool IsChat(string path) => !path.Contains("/settings", StringComparison.Ordinal)
        && !path.Contains("/usage", StringComparison.Ordinal) && !path.Contains("/models", StringComparison.Ordinal) && !path.Contains("/policy", StringComparison.Ordinal);
    private static JsonArray Permissions(string path, HttpMethod method)
    {
        if (path.StartsWith("/api/me/", StringComparison.Ordinal)) return [];
        if (path.StartsWith("/api/organization/", StringComparison.Ordinal)) return new JsonArray(IsChat(path) ? "assistant.use" : "assistant.configure");
        if (path.EndsWith("/policy", StringComparison.Ordinal)) return new JsonArray(method == HttpMethod.Get ? "platform.tenants.read" : "platform.tenants.manage");
        return new JsonArray(IsChat(path) ? "platform.assistant.use" : path.EndsWith("/usage", StringComparison.Ordinal) ? "platform.assistant.read" : "platform.assistant.configure");
    }
    private static List<HttpContractError> Errors(string path, HttpMethod method)
    {
        var list = new List<HttpContractError>
        {
            HttpContractDocumentation.CoreError(400, "Validation.Failed"),
            HttpContractDocumentation.CoreError(401, "Http.Unauthorized"),
            HttpContractDocumentation.CoreError(403, "Http.Forbidden"),
            HttpContractDocumentation.CoreError(403, "Legal.AcceptanceRequired"),
            HttpContractDocumentation.CoreError(404, "Http.NotFound"),
            HttpContractDocumentation.CoreError(500, "General.Unexpected"),
            HttpContractDocumentation.CoreError(501, "General.Contract.NotImplemented"),
        };
        if (method == HttpMethod.Post)
            list.AddRange([HttpContractDocumentation.CoreError(400, "Request.IdempotencyKeyRequired"),
                HttpContractDocumentation.CoreError(422, "Request.IdempotencyKeyReused"), HttpContractDocumentation.CoreError(409, "Request.InProgress")]);
        if (method == HttpMethod.Put || method == HttpMethod.Delete || path.Contains("/quick/", StringComparison.Ordinal) || path.EndsWith("/messages", StringComparison.Ordinal) || path.EndsWith("/attachments", StringComparison.Ordinal))
            list.Add(HttpContractDocumentation.CoreError(409, "General.ConcurrencyConflict", "version"));
        foreach (var error in ModuleErrors)
            if (Applies(error.Code, path, method)) list.Add(error);
        foreach (var (field, key) in ValidationFields(path, method))
            list.Add(new HttpContractError(400, "Validation.Failed", field, Resource("Assistant.Validation." + key, "es"), Resource("Assistant.Validation." + key, "en")));
        return list;
    }
    private static bool Applies(string code, string path, HttpMethod method)
    {
        if (code == "Assistant.Conversation.NotFound") return IsChat(path) && path.Contains("/conversations/", StringComparison.Ordinal) || path.Contains("/quick/", StringComparison.Ordinal);
        if (code.StartsWith("Assistant.Attachment.", StringComparison.Ordinal)) return path.Contains("/attachments", StringComparison.Ordinal) || path.EndsWith("/messages", StringComparison.Ordinal);
        if (code.StartsWith("Assistant.Turn.", StringComparison.Ordinal)) return path.EndsWith("/messages", StringComparison.Ordinal) || path.Contains("/turns/", StringComparison.Ordinal);
        if (code == "Assistant.Model.AlreadyExists") return path.Contains("/models", StringComparison.Ordinal) && method != HttpMethod.Get;
        if (code == "Assistant.Model.NotFound") return path.Contains("/models/", StringComparison.Ordinal);
        if (code == "Assistant.Model.NotAllowed") return path.EndsWith("/settings", StringComparison.Ordinal) && method == HttpMethod.Put || path.EndsWith("/messages", StringComparison.Ordinal);
        if (code == "Assistant.Settings.NotConfigured") return path.EndsWith("/messages", StringComparison.Ordinal);
        if (code.StartsWith("Assistant.Settings.", StringComparison.Ordinal)) return path.EndsWith("/settings", StringComparison.Ordinal) && method == HttpMethod.Put;
        if (code.StartsWith("Assistant.Limit.", StringComparison.Ordinal) || code.StartsWith("Assistant.Provider.", StringComparison.Ordinal)) return path.EndsWith("/messages", StringComparison.Ordinal);
        if (code.StartsWith("Assistant.Notice.", StringComparison.Ordinal)) return IsChat(path) && method != HttpMethod.Get;
        if (code.StartsWith("Assistant.QuickQuery.", StringComparison.Ordinal)) return path.Contains("/quick/", StringComparison.Ordinal);
        return code == "Assistant.Export.Failed" && path.EndsWith("/export", StringComparison.Ordinal);
    }
    private static IEnumerable<(string Field, string Key)> ValidationFields(string path, HttpMethod method)
    {
        if (method == HttpMethod.Put || method == HttpMethod.Delete) yield return ("version", "VersionRequired");
        if (path.EndsWith("/messages", StringComparison.Ordinal))
        { yield return ("text", "TextRequired"); yield return ("text", "TextTooLong"); yield return ("attachmentIds", "IdsInvalid"); yield return ("conversationVersion", "VersionRequired"); }
        if (path.Contains("/quick/", StringComparison.Ordinal)) yield return ("version", "QuickVersionInvalid");
        if (path.EndsWith("/attachments", StringComparison.Ordinal) && method == HttpMethod.Post)
        { yield return ("file", "FileInvalid"); yield return ("conversationVersion", "VersionRequired"); }
        if (path.EndsWith("/settings", StringComparison.Ordinal) && method == HttpMethod.Put)
        { yield return ("apiKey", "KeyRequired"); yield return ("apiKey", "KeyUnexpected"); yield return ("apiKey", "KeyTooLong"); yield return ("keyAction", "SelectionInvalid"); yield return ("effort", "SelectionInvalid"); yield return (path.StartsWith("/api/platform/", StringComparison.Ordinal) ? "defaultModelId" : "modelId", "Required"); yield return ("dailyMessageLimit", "LimitsInvalid"); yield return (path.StartsWith("/api/platform/", StringComparison.Ordinal) ? "monthlyOrganizationBudget" : "monthlyBudget", "PriceInvalid");
          if (path.StartsWith("/api/platform/", StringComparison.Ordinal)) { yield return ("allowedModelIds", "IdsInvalid"); yield return ("personalEnabled", "Required"); yield return ("retentionDays", "LimitsInvalid"); } else yield return ("mode", "SelectionInvalid"); }
        if (path.Contains("/models", StringComparison.Ordinal) && method != HttpMethod.Get)
        { yield return ("name", "Required"); yield return ("provider", "SelectionInvalid"); yield return ("providerModelId", "Required"); yield return ("inputPricePerMillion", "PriceInvalid"); yield return ("outputPricePerMillion", "PriceInvalid"); yield return ("isActive", "Required"); }
        if (path.Contains("/usage", StringComparison.Ordinal)) { yield return ("month", "MonthInvalid"); yield return ("page", "PageInvalid"); yield return ("pageSize", "PageInvalid"); }
        if (path.EndsWith("/policy", StringComparison.Ordinal) && method == HttpMethod.Put) { yield return ("allowOwnKey", "Required"); yield return ("reason", "Required"); }
        if (path.EndsWith("/notice", StringComparison.Ordinal)) yield return ("noticeVersion", "Required");
        if (path.Contains("/feedback", StringComparison.Ordinal)) yield return ("rating", "SelectionInvalid");
        if (method == HttpMethod.Put && IsChat(path) && path.Contains("/conversations/", StringComparison.Ordinal)) yield return ("title", "TitleTooLong");
    }
    private static string Description(string path, HttpMethod method)
    {
        if (path.EndsWith("/messages", StringComparison.Ordinal)) return "POST SSE autenticado, sin EventSource anónimo. Primera admisión 200 text/event-stream; replay terminal 200 application/json TurnResult. Preflight antes de abrir stream; en curso 409. Sin copia de contenido en el store global.";
        if (path.Contains("/quick/", StringComparison.Ordinal)) return "Consulta fija sin IA, títulos automáticos, cuota ni costo; revalida permisos de la herramienta. DurableReference reconstruye el resultado privado tras revalidar dueño/lado/organización/legales/módulo/permisos.";
        if (path.EndsWith("/settings", StringComparison.Ordinal)) return "Clave write-only: keyAction keep/replace/clear; apiKey solo en replace, sin key/cipher en salidas o logs. OwnKey requiere política y Dueño activo con Email verificado utilizable. PlatformKey rechaza topes propios. Gestión Plataforma accesible sin clave de chat.";
        if (path.Contains("/usage", StringComparison.Ordinal)) return "Agregados mensuales paginados YYYY-MM; sin texto, conversationId, títulos ni archivos. Costo platform-key separado de own-key.";
        if (path.Contains("/attachments", StringComparison.Ordinal)) return "Adjunto privado PNG/JPEG/WebP/PDF, un archivo multipart por upload, máximo 10485760 bytes; hasta cinco al consumir mensaje. Verificar firma y extensión. Pendiente hasta consumo, replay por referencia privada, sin storageKey o URL pública.";
        if (path.EndsWith("/export", StringComparison.Ordinal)) return "Descarga Markdown UTF-8 o PDF privada, no-store; solo conversación propia. Sin tool payloads, claves ni bytes originales de adjuntos; sin IA, cuota ni costo.";
        if (path.EndsWith("/notice", StringComparison.Ordinal)) return "Consentimiento explícito por usuario y versión, después de aceptar documentos legales vigentes; 204, sin inferir aceptación.";
        if (path.Contains("/turns/", StringComparison.Ordinal)) return "Stop idempotente y durable. RequestAborted solicita stop; la admisión persistida no se deshace.";
        if (path.EndsWith("/policy", StringComparison.Ordinal)) return "Gestión global sin abrir conversaciones ni devolver claves. Ausente: allowOwnKey=false/version=0; PUT requiere motivo y SecurityEvent, revalida configuración/turnos.";
        if (path.Contains("/models", StringComparison.Ordinal)) return $"Catálogo Anthropic, precios no negativos en {AssistantContractPolicy.BillingCurrency}, unicidad provider/providerModelId. Desactivar modelos referidos; sin endpoint de borrado.";
        if (path.EndsWith("/conversations", StringComparison.Ordinal)) return method == HttpMethod.Get
            ? "Cursor opaco ligado a lado/organización/dueño/filtro; orden LastActivityAtUtc/Id descendente, limit=20 máximo100."
            : "Crea sin IA, title=null/isSaved=false; 201 con Location. DurableReference no copia título al store global.";
        if (path.Contains("/feedback", StringComparison.Ordinal)) return "Solo respuesta assistant propia; up/down/null y versión. Sin detalle libre.";
        if (path.Contains("/conversations/", StringComparison.Ordinal)) return method == HttpMethod.Get
            ? "Conversación propia, mensajes cronológicos por cursor; limit=50 máximo100. Ajeno devuelve404."
            : "Edición/borrado con uint/xmin y owner firmado; borrar cancela turno y elimina textos/adjuntos con limpieza física diferida fiable.";
        return "Estado efectivo: apagado404; habilitado sin clave configured=false. remainingMessages=null si >10. Retención30 días salvo guardados, maxAttachmentBytes10485760/maxAttachments5.";
    }
    private static Dictionary<string, JsonNode>? RequestExamples(string path, HttpMethod method)
    {
        if (method == HttpMethod.Get || method == HttpMethod.Delete) return null;
        var body = path.EndsWith("/notice", StringComparison.Ordinal) ? "{\"noticeVersion\":\"2026-10-08\"}"
            : path.EndsWith("/messages", StringComparison.Ordinal) ? "{\"text\":\"¿Cuántas personas hay?\",\"attachmentIds\":[],\"conversationVersion\":1}"
            : path.Contains("/feedback", StringComparison.Ordinal) ? "{\"rating\":\"up\",\"version\":1}"
            : path.Contains("/quick/", StringComparison.Ordinal) ? "{}"
            : path.EndsWith("/policy", StringComparison.Ordinal) ? "{\"allowOwnKey\":false,\"version\":0,\"reason\":\"Política revisada\"}"
            : path.Contains("/models", StringComparison.Ordinal) ? "{\"name\":\"Modelo de ejemplo\",\"provider\":\"Anthropic\",\"providerModelId\":\"model-contract-example\",\"inputPricePerMillion\":{\"amount\":3},\"outputPricePerMillion\":{\"amount\":15},\"isActive\":true" + (method == HttpMethod.Put ? ",\"version\":1}" : "}")
            : path.EndsWith("/settings", StringComparison.Ordinal) ? path.StartsWith("/api/platform/", StringComparison.Ordinal)
                ? "{\"defaultModelId\":\"10000000-0000-0000-0000-000000000001\",\"allowedModelIds\":[\"10000000-0000-0000-0000-000000000001\"],\"effort\":\"medium\",\"dailyMessageLimit\":50,\"monthlyOrganizationBudget\":{\"amount\":20},\"personalEnabled\":false,\"retentionDays\":30,\"keyAction\":\"keep\",\"version\":1}"
                : "{\"mode\":\"PlatformKey\",\"modelId\":\"10000000-0000-0000-0000-000000000001\",\"effort\":\"medium\",\"keyAction\":\"keep\",\"version\":1}"
            : method == HttpMethod.Put ? "{\"title\":\"Personas activas\",\"isSaved\":true,\"version\":1}" : "{}";
        var example = JsonNode.Parse(body)!;
        foreach (var field in new[] { "inputPricePerMillion", "outputPricePerMillion", "monthlyOrganizationBudget" })
            if (example[field] is JsonObject money) money["currency"] = AssistantContractPolicy.BillingCurrency;
        return new Dictionary<string, JsonNode> { ["request"] = example };
    }
    private static void AddSuccessExample(IOpenApiResponse response, string path, HttpMethod method, OpenApiDocument document)
    {
        foreach (var (_, media) in response.Content ?? new Dictionary<string, OpenApiMediaType>())
        {
            if (media.Schema is null) continue;
            media.Examples = new Dictionary<string, IOpenApiExample> { ["success"] = new OpenApiExample { Value = SuccessExample(media.Schema, document, path, method) } };
        }
    }
    private static JsonNode? SuccessExample(IOpenApiSchema schema, OpenApiDocument document, string path, HttpMethod method)
    {
        var example = Example(schema, document, 0);
        if (method == HttpMethod.Post && path.EndsWith("/conversations", StringComparison.Ordinal) && example is JsonObject created)
            created["title"] = null;
        if (schema is OpenApiSchemaReference { Reference.Id: "OrganizationAssistantSettingsResponse" or "PlatformAssistantSettingsResponse" }
            && example is JsonObject settings && settings["hasKey"]?.GetValue<bool>() == false)
            settings["maskedKeySuffix"] = null;
        if (schema is OpenApiSchemaReference { Reference.Id: "AssistantQuickResponse" } && example is JsonObject quick)
        {
            var user = Example(new OpenApiSchemaReference("AssistantMessageResponse", document), document, 0)!.AsObject();
            user["role"] = "user";
            user["text"] = "Consulta rápida";
            var assistant = Example(new OpenApiSchemaReference("AssistantMessageResponse", document), document, 0)!.AsObject();
            assistant["id"] = "10000000-0000-0000-0000-000000000002";
            assistant["text"] = "Resultado de la consulta";
            quick["messages"] = new JsonArray(user, assistant);
        }
        return example;
    }

    private static JsonNode? Example(IOpenApiSchema schema, OpenApiDocument document, int depth)
    {
        if (depth > 12) return null;
        if (schema is OpenApiSchemaReference reference && document.Components?.Schemas?.TryGetValue(reference.Reference.Id!, out var actual) == true)
        {
            var value = Example(actual, document, depth + 1);
            if (reference.Reference.Id == "AssistantMessageResponse" && value is JsonObject message)
            { message["role"] = "assistant"; message["status"] = "Complete"; }
            return value;
        }
        if (schema.Type == JsonSchemaType.Null) return null;
        if (schema.Enum?.Count > 0) return schema.Enum[0]?.DeepClone();
        if (schema.OneOf?.Count > 0) return Example(schema.OneOf[0], document, depth + 1);
        if (schema.AnyOf?.Count > 0) return Example(schema.AnyOf[0], document, depth + 1);
        if (schema.Type?.HasFlag(JsonSchemaType.Object) == true || schema.Properties?.Count > 0)
        {
            var node = new JsonObject();
            foreach (var (key, property) in schema.Properties ?? new Dictionary<string, IOpenApiSchema>())
                if (property.WriteOnly != true) node[key] = key switch
                {
                    "currency" => JsonValue.Create(AssistantContractPolicy.BillingCurrency),
                    "month" => JsonValue.Create("2026-10"),
                    "dailyLimit" or "dailyMessageLimit" => JsonValue.Create(50),
                    "retentionDays" => JsonValue.Create(30),
                    "maxAttachmentBytes" => JsonValue.Create(10485760),
                    "maxAttachments" => JsonValue.Create(5),
                    "noticeVersion" => JsonValue.Create("2026-10-08"),
                    _ => Example(property, document, depth + 1),
                };
            return node;
        }
        if (schema.Type?.HasFlag(JsonSchemaType.Array) == true) return new JsonArray();
        if (schema.Type?.HasFlag(JsonSchemaType.Boolean) == true) return JsonValue.Create(false);
        if (schema.Type?.HasFlag(JsonSchemaType.Integer) == true || schema.Type?.HasFlag(JsonSchemaType.Number) == true) return JsonValue.Create(1);
        return JsonValue.Create(schema.Format == "uuid" ? "10000000-0000-0000-0000-000000000001" : schema.Format == "date-time" ? "2026-10-09T12:00:00Z" : schema.Format == "date" ? "2026-10-09" : "example");
    }
    private static void DescribeStream(OpenApiOperation operation, OpenApiDocument document)
    {
        operation.Responses!["200"] = new OpenApiResponse
        {
            Description = "Primera admisión: SSE; replay terminal: JSON privado reautorizado.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["text/event-stream"] = new() { Schema = new OpenApiSchemaReference("AssistantStreamHttpResponse", document) },
                ["application/json"] = new() { Schema = new OpenApiSchemaReference("AssistantTurnResultResponse", document), Examples = new Dictionary<string, IOpenApiExample> { ["terminalReplay"] = new OpenApiExample { Value = Example(document.Components!.Schemas!["AssistantTurnResultResponse"], document, 0) } } },
            },
        };
        var events = new JsonArray();
        foreach (var (eventName, payload) in new[] { ("start", "AssistantStartEventResponse"), ("delta", "AssistantDeltaEventResponse"), ("tool", "AssistantToolEventResponse"), ("title", "AssistantTitleEventResponse"), ("done", "AssistantTurnResultResponse"), ("error", "AssistantErrorEventResponse") })
            events.Add(new JsonObject { ["event"] = eventName, ["schema"] = new JsonObject { ["$ref"] = "#/components/schemas/" + payload }, ["example"] = Example(document.Components!.Schemas![payload], document, 0) });
        operation.Extensions!["x-sse-events"] = new JsonNodeExtension(events);
        operation.Extensions["x-sse-wire"] = new JsonNodeExtension(new JsonObject { ["encoding"] = "UTF-8", ["id"] = "<turnId>:<sequence>", ["heartbeatSeconds"] = 15, ["data"] = "payload JSON only; {event,data} is the typed transport/OpenAPI envelope", ["cacheControl"] = "no-store", ["flush"] = "per fragment", ["terminal"] = "done|error" });
    }
    private static void DescribeFiles(OpenApiOperation operation, string path, HttpMethod method)
    {
        if (path.EndsWith("/attachments", StringComparison.Ordinal) && method == HttpMethod.Post)
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Required = new HashSet<string>(StringComparer.Ordinal) { "file", "conversationVersion" },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary", Description = "Un archivo PNG/JPEG/WebP/PDF, hasta 10485760 bytes." },
                                ["conversationVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "uint32", Minimum = "0", Maximum = "4294967295" },
                            },
                        },
                        Examples = new Dictionary<string, IOpenApiExample> { ["upload"] = new OpenApiExample { Description = "FormData: file contiene un archivo y conversationVersion=1; no JSON/base64.", Value = new JsonObject { ["file"] = "(binary PNG/JPEG/WebP/PDF)", ["conversationVersion"] = 1 } } },
                    },
                },
            };
        }
        if (method != HttpMethod.Get || !path.EndsWith("/export", StringComparison.Ordinal) && !path.Contains("/attachments/", StringComparison.Ordinal)) return;
        if (operation.Responses!["200"] is not OpenApiResponse response) return;
        var mediaTypes = path.EndsWith("/export", StringComparison.Ordinal) ? new[] { "text/markdown", "application/pdf" }
            : ["image/png", "image/jpeg", "image/webp", "application/pdf"];
        response.Content = mediaTypes.ToDictionary(value => value, value => new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
            Examples = new Dictionary<string, IOpenApiExample> { ["download"] = new OpenApiExample { Description = "Bytes privados del formato solicitado; filename autorizado en Content-Disposition.", Value = JsonValue.Create(value == "text/markdown" ? "# Conversación\n\nConsulta y respuesta." : "(binary content)") } },
        });
    }
    private static void DescribeQuery(OpenApiOperation operation, string path)
    {
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            if (parameter.In != ParameterLocation.Query || string.IsNullOrEmpty(parameter.Name)) continue;
            parameter.Name = char.ToLowerInvariant(parameter.Name[0]) + parameter.Name[1..];
            if (parameter.Schema is OpenApiSchemaReference reference)
                parameter.Schema = new OpenApiSchema { AllOf = [reference] };
            if (parameter.Schema is not OpenApiSchema schema) continue;
            switch (parameter.Name)
            {
                case "month":
                    parameter.Required = true;
                    parameter.Example = JsonValue.Create("2026-10");
                    schema.Type = JsonSchemaType.String;
                    schema.Pattern = "^(?!0000)[0-9]{4}-(0[1-9]|1[0-2])$";
                    break;
                case "version":
                    parameter.Required = true;
                    schema.Type = JsonSchemaType.Integer;
                    schema.Format = "uint32";
                    schema.Minimum = "0";
                    schema.Maximum = "4294967295";
                    break;
                case "limit":
                    schema.Minimum = "1"; schema.Maximum = "100";
                    schema.Default = JsonValue.Create(path.EndsWith("/conversations", StringComparison.Ordinal) ? 20 : 50);
                    break;
                case "page": schema.Minimum = "1"; schema.Default = JsonValue.Create(1); break;
                case "pageSize":
                    schema.Minimum = "10"; schema.Maximum = "100";
                    schema.Enum = [JsonValue.Create(10)!, JsonValue.Create(20)!, JsonValue.Create(50)!, JsonValue.Create(100)!];
                    schema.Default = JsonValue.Create(20);
                    break;
                case "format": schema.Default = JsonValue.Create("markdown"); break;
                case "groupBy": schema.Default = JsonValue.Create("day"); break;
                case "search": schema.MaxLength = 100; break;
            }
        }
    }

    private static void DescribeHeaders(OpenApiOperation operation, string path)
    {
        foreach (var (status, response) in operation.Responses ?? [])
        {
            if (response is not OpenApiResponse editable) continue;
            if (status == "201" && !path.EndsWith("/models", StringComparison.Ordinal))
            { editable.Headers ??= new Dictionary<string, IOpenApiHeader>(); editable.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String }, Description = "Ruta privada del recurso creado." }; }
            if (status == "503")
            { editable.Headers ??= new Dictionary<string, IOpenApiHeader>(); editable.Headers["Retry-After"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.Integer }, Description = "Segundos hasta el siguiente intento." }; }
            if (status == "200" && (path.Contains("/attachments/", StringComparison.Ordinal) || path.EndsWith("/export", StringComparison.Ordinal) || path.EndsWith("/messages", StringComparison.Ordinal)))
            {
                editable.Headers ??= new Dictionary<string, IOpenApiHeader>(); editable.Headers["Cache-Control"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create("no-store")!] } };
                if (!path.EndsWith("/messages", StringComparison.Ordinal)) editable.Headers["Content-Disposition"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String }, Description = "attachment; filename autorizado y escapado." };
            }
        }
    }
    private static IOpenApiSchema ReplaceMoneyReferences(IOpenApiSchema schema, OpenApiDocument document)
    {
        if (schema is OpenApiSchemaReference reference)
            return reference.Reference.Id == "Money" ? new OpenApiSchemaReference("AssistantMoney", document) : schema;
        if (schema is not OpenApiSchema mutable) return schema;
        foreach (var key in mutable.Properties?.Keys.ToArray() ?? [])
            mutable.Properties![key] = ReplaceMoneyReferences(mutable.Properties[key], document);
        if (mutable.Items is not null) mutable.Items = ReplaceMoneyReferences(mutable.Items, document);
        if (mutable.OneOf is not null) mutable.OneOf = mutable.OneOf.Select(value => ReplaceMoneyReferences(value, document)).ToList();
        if (mutable.AnyOf is not null) mutable.AnyOf = mutable.AnyOf.Select(value => ReplaceMoneyReferences(value, document)).ToList();
        if (mutable.AllOf is not null) mutable.AllOf = mutable.AllOf.Select(value => ReplaceMoneyReferences(value, document)).ToList();
        return mutable;
    }

    private static void DescribeSchemas(OpenApiDocument document)
    {
        var schemas = document.Components!.Schemas!;
        schemas["AssistantMoney"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalPropertiesAllowed = false,
            Required = new HashSet<string>(StringComparer.Ordinal) { "amount", "currency" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["amount"] = new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" },
                ["currency"] = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^[A-Z]{3}$", MinLength = 3, MaxLength = 3 },
            },
        };
        foreach (var (name, schema) in schemas.Where(pair => pair.Key.Contains("Assistant", StringComparison.Ordinal)))
        {
            if (schema is not OpenApiSchema mutable) continue;
            ReplaceMoneyReferences(mutable, document);
            if (name.StartsWith("AssistantStreamHttpResponseAssistant", StringComparison.Ordinal))
            {
                mutable.Type = JsonSchemaType.Object;
                mutable.AdditionalPropertiesAllowed = false;
                mutable.Required ??= new HashSet<string>(StringComparer.Ordinal);
                mutable.Required.Add("event");
                mutable.Required.Add("data");
            }
            if (name.StartsWith("AssistantQuickDataResponseAssistant", StringComparison.Ordinal))
            {
                mutable.Type = JsonSchemaType.Object;
                mutable.AdditionalPropertiesAllowed = false;
            }
            if (mutable.Properties?.TryGetValue("apiKey", out var secret) == true && secret is OpenApiSchema property) property.WriteOnly = true;
            if (mutable.Properties?.TryGetValue("version", out var version) == true && version is OpenApiSchema versionSchema) { versionSchema.Minimum = "0"; versionSchema.Maximum = "4294967295"; }
            if (name == "AssistantStreamHttpResponse")
            {
                mutable.AnyOf = null;
                mutable.OneOf = StreamNames.Select(value => (IOpenApiSchema)new OpenApiSchemaReference("AssistantStreamHttpResponseAssistant" + value + "StreamHttpResponse", document)).ToList();
                mutable.Discriminator = new OpenApiDiscriminator { PropertyName = "event", Mapping = StreamNames.ToDictionary(value => value.ToLowerInvariant(), value => new OpenApiSchemaReference("AssistantStreamHttpResponseAssistant" + value + "StreamHttpResponse", document)) };
            }
            if (name == "AssistantQuickDataResponse" && mutable.AnyOf is { } variants)
            { mutable.OneOf = variants; mutable.AnyOf = null; mutable.Discriminator = null; }
        }
        if (schemas["ProblemDetails"] is OpenApiSchema problem)
        {
            problem.Properties!["dailyLimit"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null };
            problem.Properties["dailyResetAtUtc"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date-time" };
        }
    }
}
