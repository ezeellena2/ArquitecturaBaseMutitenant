using System.Globalization;
using System.Resources;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp;

/// <summary>Documenta las rutas de atención (E11): acceso, permiso, módulo, errores y topes de las listas anidadas.</summary>
internal sealed class WhatsAppAttentionContractContributor : IHttpContractContributor
{
    private static readonly ResourceManager Resources = new("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources.WhatsAppTexts", typeof(WhatsAppWebhookRequest).Assembly);

    private static readonly (string Schema, string Property, int MaxItems)[] Bounds =
    [
        ("WhatsAppBotResponse", "menu", WhatsAppFeature.MaxMenuOptions), ("PlatformWhatsAppBotResponse", "menu", WhatsAppFeature.MaxMenuOptions),
        ("WhatsAppBotSchedule", "days", 7), ("WhatsAppBotScheduleInput", "days", 7),
        ("UpdateWhatsAppBotHttpRequest", "menu", WhatsAppFeature.MaxMenuOptions), ("UpdatePlatformWhatsAppBotHttpRequest", "menu", WhatsAppFeature.MaxMenuOptions),
        ("WhatsAppContactResponse", "tags", WhatsAppFeature.MaxContactTags), ("UpdateWhatsAppContactHttpRequest", "tags", WhatsAppFeature.MaxContactTags),
        ("WhatsAppTemplateResponse", "variables", WhatsAppFeature.MaxTemplateVariables), ("WhatsAppTemplateOptionResponse", "variables", WhatsAppFeature.MaxTemplateVariables),
        ("SendWhatsAppTemplateHttpRequest", "variables", WhatsAppFeature.MaxTemplateVariables), ("CreateWhatsAppTemplateHttpRequest", "variables", WhatsAppFeature.MaxTemplateVariables),
        ("CreateWhatsAppBroadcastHttpRequest", "variables", WhatsAppFeature.MaxTemplateVariables),
        ("WhatsAppPhoneImportHttpRequest", "phones", WhatsAppFeature.MaxPhoneImport), ("WhatsAppPhoneImportPreviewResponse", "rows", WhatsAppFeature.MaxPhoneImport),
    ];

    private static readonly (string Schema, string Property, int MaxLength)[] Lengths =
    [
        ("WhatsAppBotMenuOptionInput", "label", WhatsAppTextLimits.MenuLabel), ("WhatsAppBotMenuOptionInput", "reply", WhatsAppTextLimits.BotText),
        ("WhatsAppTemplateVariableInput", "label", TextLimits.ShortName), ("WhatsAppTemplateVariableInput", "example", WhatsAppTextLimits.TemplateVariable),
        ("WhatsAppAudienceInput", "tag", WhatsAppTextLimits.Tag),
    ];

    public void Describe(OpenApiDocument document)
    {
        if (document.Components?.Schemas is { } schemas)
        {
            foreach (var (schema, property, maxItems) in Bounds)
                if (Property(schemas, schema, property) is { } value) value.MaxItems = maxItems;
            foreach (var (schema, property, maxLength) in Lengths)
                if (Property(schemas, schema, property) is { } value) value.MaxLength = maxLength;
            foreach (var (schema, property) in new[] { ("UpdateWhatsAppContactHttpRequest", "tags"), ("WhatsAppContactResponse", "tags") })
                if (Property(schemas, schema, property)?.Items is OpenApiSchema item) item.MaxLength = WhatsAppTextLimits.Tag;
            foreach (var schema in new[] { "SendWhatsAppTemplateHttpRequest", "CreateWhatsAppBroadcastHttpRequest" })
                if (Property(schemas, schema, "variables")?.Items is OpenApiSchema item) item.MaxLength = WhatsAppTextLimits.TemplateVariable;
        }

        foreach (var (path, item) in document.Paths.Where(pair => IsOwnPath(pair.Key)))
            foreach (var (method, operation) in item.Operations ?? [])
            {
                HttpContractDocumentation.Describe(document, path, method,
                    "Atención por WhatsApp (ADR 0038). La organización sale del token y el número de Meta; nunca del cuerpo.",
                    Errors(path, method), idempotent: method == HttpMethod.Post && IsCreation(path), authenticated: true);
                operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                operation.Extensions["x-access"] = new JsonNodeExtension(JsonValue.Create(path.StartsWith("/api/me/", StringComparison.Ordinal) ? "consumer"
                    : path.StartsWith("/api/organization/", StringComparison.Ordinal) ? "business" : "platform")!);
                operation.Extensions["x-permissions"] = new JsonNodeExtension(new JsonArray(Permission(path) is { } permission ? [JsonValue.Create(permission)] : []));
                operation.Extensions["x-module-gate"] = new JsonNodeExtension(JsonValue.Create(path.StartsWith("/api/platform/", StringComparison.Ordinal)
                    ? "installed-management" : "feature:" + WhatsAppFeature.Key)!);
            }
    }

    private static bool IsOwnPath(string path) => path.StartsWith("/api/organization/whatsapp/", StringComparison.Ordinal)
        || path.StartsWith("/api/platform/whatsapp/", StringComparison.Ordinal) || path.StartsWith("/api/me/whatsapp/", StringComparison.Ordinal);

    private static bool IsCreation(string path) => !path.EndsWith("/take", StringComparison.Ordinal) && !path.EndsWith("/transfer", StringComparison.Ordinal)
        && !path.EndsWith("/check", StringComparison.Ordinal) && !path.EndsWith("/cancel", StringComparison.Ordinal) && !path.EndsWith("/preview", StringComparison.Ordinal);

    private static string? Permission(string path) => path switch
    {
        _ when path.StartsWith("/api/me/", StringComparison.Ordinal) => null,
        _ when path.StartsWith("/api/platform/whatsapp/conversations", StringComparison.Ordinal) => PlatformPermissions.WhatsApp.Attend,
        _ when path.StartsWith("/api/platform/", StringComparison.Ordinal) => PlatformPermissions.WhatsApp.Manage,
        _ when path.StartsWith("/api/organization/whatsapp/customers", StringComparison.Ordinal) => Permissions.WhatsApp.Customers,
        _ when path.StartsWith("/api/organization/whatsapp/internal", StringComparison.Ordinal) => Permissions.WhatsApp.Internal,
        _ when path.StartsWith("/api/organization/whatsapp/contacts", StringComparison.Ordinal) => Permissions.WhatsApp.Customers,
        _ when path.StartsWith("/api/organization/whatsapp/broadcasts", StringComparison.Ordinal) => Permissions.WhatsApp.Broadcasts,
        _ when path.StartsWith("/api/organization/whatsapp/phone-imports", StringComparison.Ordinal) => Permissions.Users.Invite,
        _ => Permissions.WhatsApp.Manage,
    };

    private static List<HttpContractError> Errors(string path, HttpMethod method)
    {
        var errors = new List<HttpContractError>();
        void Add(int status, string code, string? field = null) => errors.Add(new HttpContractError(status, code, field,
            Resources.GetString(code, CultureInfo.GetCultureInfo("es")) ?? code, Resources.GetString(code, CultureInfo.GetCultureInfo("en")) ?? code));
        var connect = method == HttpMethod.Post && (path.EndsWith("/numbers", StringComparison.Ordinal) || path.EndsWith("/whatsapp/number", StringComparison.Ordinal));
        if (connect)
        {
            Add(409, WhatsAppErrors.NumberAlreadyConnectedCode);
            Add(400, WhatsAppErrors.NumberSignupExpiredCode, "code");
            Add(503, WhatsAppErrors.NumberMetaRejectedCode);
            return errors;
        }
        if (path.Contains("/numbers/{id}", StringComparison.Ordinal) || path.EndsWith("/number/check", StringComparison.Ordinal)
            || path.EndsWith("/whatsapp/number", StringComparison.Ordinal) && method == HttpMethod.Put)
            Add(404, WhatsAppErrors.NumberNotFoundCode);
        if (path.EndsWith("/check", StringComparison.Ordinal)) Add(503, WhatsAppErrors.NumberMetaRejectedCode);
        if (path.EndsWith("/bot", StringComparison.Ordinal) && method == HttpMethod.Put)
        {
            Add(400, WhatsAppErrors.BotTooManyOptionsCode, "menu");
            Add(404, WhatsAppErrors.TeamNotFoundCode);
        }
        if (path.Contains("/teams", StringComparison.Ordinal))
        {
            if (path.Contains("{id}", StringComparison.Ordinal)) Add(404, WhatsAppErrors.TeamNotFoundCode);
            if (method == HttpMethod.Post || method == HttpMethod.Put) Add(409, WhatsAppErrors.TeamNameTakenCode, "name");
        }
        if (path.Contains("/conversations/{id}", StringComparison.Ordinal))
        {
            Add(404, path.StartsWith("/api/me/", StringComparison.Ordinal) ? WhatsAppErrors.LinkNotFoundCode : WhatsAppErrors.ConversationNotFoundCode);
            if (path.EndsWith("/take", StringComparison.Ordinal) || path.EndsWith("/messages", StringComparison.Ordinal) || path.EndsWith("/templates", StringComparison.Ordinal))
            {
                Add(409, WhatsAppErrors.ConversationTakenByOtherCode);
                Add(409, WhatsAppErrors.ConversationReadOnlyCode);
            }
            if (path.EndsWith("/messages", StringComparison.Ordinal)) Add(409, WhatsAppErrors.ConversationWindowClosedCode);
            if (path.EndsWith("/messages", StringComparison.Ordinal) || path.EndsWith("/templates", StringComparison.Ordinal))
                Add(409, WhatsAppErrors.ConversationPaymentRequiredCode);
            if (path.EndsWith("/templates", StringComparison.Ordinal)) Add(409, WhatsAppErrors.TemplateNotApprovedCode, "templateId");
            if (path.EndsWith("/transfer", StringComparison.Ordinal)) Add(404, WhatsAppErrors.TeamNotFoundCode);
        }
        if (path.StartsWith("/api/me/whatsapp/links/", StringComparison.Ordinal)) Add(404, WhatsAppErrors.LinkNotFoundCode);
        if (path.Contains("/contacts/{id}", StringComparison.Ordinal)) Add(404, WhatsAppErrors.ContactNotFoundCode);
        if (path.EndsWith("/contacts/import", StringComparison.Ordinal))
        {
            Add(400, WhatsAppErrors.ImportInvalidFileCode, "file");
            Add(400, WhatsAppErrors.ImportTooManyRowsCode, "file");
        }
        if (path.Contains("/templates", StringComparison.Ordinal) && !path.Contains("/conversations/", StringComparison.Ordinal))
        {
            if (path.Contains("{id}", StringComparison.Ordinal)) Add(404, WhatsAppErrors.TemplateNotFoundCode);
            if (method == HttpMethod.Post)
            {
                Add(409, WhatsAppErrors.TemplateNameTakenCode, "name");
                Add(503, WhatsAppErrors.NumberMetaRejectedCode);
            }
        }
        if (path.Contains("/broadcasts", StringComparison.Ordinal))
        {
            if (path.Contains("{id}", StringComparison.Ordinal)) Add(404, WhatsAppErrors.BroadcastNotFoundCode);
            if (path.EndsWith("/broadcasts", StringComparison.Ordinal) && method == HttpMethod.Post || path.EndsWith("/estimate", StringComparison.Ordinal))
            {
                Add(400, WhatsAppErrors.BroadcastNoRecipientsCode, "audience");
                Add(409, WhatsAppErrors.TemplateNotApprovedCode, "templateId");
            }
            if (path.EndsWith("/broadcasts", StringComparison.Ordinal) && method == HttpMethod.Post) Add(409, WhatsAppErrors.ConversationPaymentRequiredCode);
            if (path.EndsWith("/cancel", StringComparison.Ordinal)) Add(409, WhatsAppErrors.BroadcastNotCancellableCode);
        }
        if (path.EndsWith("/phone-imports", StringComparison.Ordinal) && method == HttpMethod.Post)
            errors.Add(HttpContractDocumentation.CoreError(503, InvitationErrors.ChannelUnavailableCode));
        return errors;
    }

    private static OpenApiSchema? Property(IDictionary<string, IOpenApiSchema> schemas, string schema, string property) =>
        schemas.TryGetValue(schema, out var value) && value is OpenApiSchema found && found.Properties?.TryGetValue(property, out var member) == true
            ? member as OpenApiSchema : null;
}
