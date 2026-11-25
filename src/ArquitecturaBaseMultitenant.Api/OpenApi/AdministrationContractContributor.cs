using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>
/// Contrato de la segunda parte de la base de la 12 en el núcleo: nadie da lo que no tiene (ADR 0061), lo incluido (260) y
/// lo que se puede dar (269), los topes de correo (0063 y 256), el estado de entrega de una invitación (0066 §4) y los plazos
/// y el nombre de los correos de Plataforma (272 y 273). WhatsApp y el asistente documentan lo suyo en su módulo.
/// </summary>
internal sealed class AdministrationContractContributor : IHttpContractContributor
{
    /// <summary>Los pedidos que mandan un código por correo (ADR 0063).</summary>
    public static readonly string[] EmailCodePaths =
    [
        "/api/auth/login-code", "/api/auth/signup", "/api/auth/business-signup/code", "/api/account-recovery/code",
        "/api/me/login-methods", "/api/me/login-methods/{methodId}/code",
    ];

    /// <summary>
    /// Las invitaciones por correo y sus reenvíos, en los tres alcances (ADR 0063 y decisión 256), y la del Dueño al crear
    /// una organización desde Plataforma, que también es una invitación por correo.
    /// </summary>
    public static readonly string[] EmailInvitationPaths =
    [
        "/api/users/invitations", "/api/users/invitations/{invitationId}/resend", "/api/companies/{companyId}/members",
        "/api/companies/{companyId}/invitations/{invitationId}/resend", "/api/platform/operators/invitations",
        "/api/platform/operators/{id}/invitation/resend", "/api/platform/tenants",
    ];

    private const string Delivery = "invitationDelivery (0066 §4): el estado de entrega de la invitación pendiente, derivado de la cola al leer: Pending (en la cola), Sent (salió, o sin fila en la cola), Uncertain («Sin confirmar»: no se sabe si salió; reenviar pide revisar antes con la persona) o Failed (no salió). null sin invitación pendiente.";
    private const string Includes = "Lo que tiene una cuenta son los permisos de sus roles (o los de un operador) más lo que esos incluyen, en cadena (260); se guarda lo pedido más lo incluido, y el GET devuelve eso.";

    public void Describe(OpenApiDocument document)
    {
        DescribeDelegation(document);
        DescribeEmailQuota(document);
        DescribeReads(document);
        DescribeSettings(document);
        DescribeSchemas(document);
    }

    private static void DescribeDelegation(OpenApiDocument document)
    {
        // Roles de la organización y de una empresa (el alcance va en el cuerpo).
        foreach (var (path, method) in new[] { ("/api/roles", HttpMethod.Post), ("/api/roles/{id}", HttpMethod.Put) })
            Describe(document, path, method, "ADR 0061: nadie da ni quita un permiso que no tiene en ese alcance. " + Includes,
                [E(403, RoleErrors.PermissionsNotHeldCode)]);
        Describe(document, "/api/users/invitations", HttpMethod.Post, "ADR 0061: no se da un rol con permisos que quien invita no tiene.",
            [E(403, MemberErrors.RolesNotHeldCode)]);
        Describe(document, "/api/users/{memberId}", HttpMethod.Put,
            "ADR 0061: no se da ni se quita un rol con permisos que quien edita no tiene, ni se edita a quien tiene permisos que quien edita no tiene. Sobre uno mismo pasa.",
            [E(403, MemberErrors.RolesNotHeldCode), E(403, MemberErrors.HasMorePermissionsCode)]);
        foreach (var (path, method) in new[] { ("/api/users/{memberId}/status", HttpMethod.Put), ("/api/users/{memberId}", HttpMethod.Delete) })
            Describe(document, path, method, "ADR 0061: no se desactiva ni se quita a quien tiene permisos que quien actúa no tiene.",
                [E(403, MemberErrors.HasMorePermissionsCode)]);
        Describe(document, "/api/companies/{companyId}/members", HttpMethod.Post,
            "ADR 0061: no se da un rol de la empresa con permisos que quien suma no tiene en ella.",
            [E(403, CompanyMembershipErrors.RolesNotHeldCode)]);
        Describe(document, "/api/companies/{companyId}/members/{memberId}/roles", HttpMethod.Put,
            "ADR 0061: no se da ni se quita un rol de la empresa con permisos que quien edita no tiene en ella, ni se edita a quien tiene en la empresa permisos que quien edita no tiene.",
            [E(403, CompanyMembershipErrors.RolesNotHeldCode), E(403, CompanyMembershipErrors.HasMorePermissionsCode)]);
        Describe(document, "/api/companies/{companyId}/members/{memberId}/status", HttpMethod.Put,
            "ADR 0061: no se desactiva a quien tiene en la empresa permisos que quien actúa no tiene.",
            [E(403, CompanyMembershipErrors.HasMorePermissionsCode)]);
        foreach (var (path, method) in new[] { ("/api/platform/operators/invitations", HttpMethod.Post), ("/api/platform/accounts/{id}/operator", HttpMethod.Put) })
            Describe(document, path, method, "ADR 0061: nadie da ni quita a un operador un permiso que no tiene. " + Includes,
                [E(403, PlatformOperatorErrors.PermissionsNotHeldCode)]);
        foreach (var (path, method) in new[]
        {
            ("/api/platform/accounts/{id}/suspend", HttpMethod.Post), ("/api/platform/accounts/{id}/reactivate", HttpMethod.Post),
            ("/api/platform/accounts/{id}/deletion", HttpMethod.Post), ("/api/platform/accounts/{id}/operator", HttpMethod.Delete),
        })
            Describe(document, path, method, "ADR 0061: sobre un operador, quien actúa tiene que tener todos sus permisos.",
                [E(403, PlatformOperatorErrors.HasMorePermissionsCode)]);
    }

    private static void DescribeEmailQuota(OpenApiDocument document)
    {
        const string Quota = "Por correo (ADR 0063 y 256): un tope global y los de cada código o invitación cuentan las últimas 24 horas, por horas; al llegar, 429 Messaging.Email.DailyLimit con retryAfter (segundos, con una hora de precisión) y Retry-After, sin mandar ni guardar nada. Lo que va por WhatsApp tiene los límites de Meta.";
        foreach (var path in EmailCodePaths.Concat(EmailInvitationPaths))
            Describe(document, path, HttpMethod.Post, Quota, [E(429, EmailQuotaErrors.DailyLimitCode)]);
    }

    private static void DescribeReads(OpenApiDocument document)
    {
        Append(document, "/api/permissions", HttpMethod.Get,
            "Cada permiso trae includes (260): los códigos que incluye, de su misma área y su mismo catálogo ([] si no incluye nada), y grantable (269): si quien pide lo tiene, para darlo.");
        foreach (var path in new[] { "/api/roles/paged", "/api/companies/{companyId}/roles/paged" })
            Append(document, path, HttpMethod.Get, "Cada rol trae grantable (269 y ADR 0061): si quien pide puede darlo ahí.");
        foreach (var path in new[] { "/api/users/paged", "/api/users/{memberId}", "/api/companies/{companyId}/members/paged", "/api/platform/accounts/paged", "/api/platform/accounts/{id}" })
            Append(document, path, HttpMethod.Get, Delivery);
        Describe(document, "/api/platform/permissions", HttpMethod.Get,
            "El catálogo de permisos de un operador, con los de los módulos instalados: code, includes (260) y grantable (269). Nombres y orden salen del catálogo del front.",
            [], authenticated: true);
        Append(document, "/api/platform/audit", HttpMethod.Get,
            "loginAuditRetentionMonths (272): los meses que se guardan los ingresos, de la configuración de Plataforma; lo nombra la (i) de la pantalla.");
    }

    private static void DescribeSettings(OpenApiDocument document)
    {
        const string Settings = "/api/platform/settings";
        Append(document, Settings, HttpMethod.Get,
            "Plazos (272): loginAuditRetentionMonths, loginCodeRetentionDays y closedInvitationRetentionDays. Nombre y correos (273): appName y senderName (sin guardar, los de la configuración de arranque), emailLogoUrl (null sin logo) y senderAddress, la dirección del «De» que fija la instalación y no se edita (null sin configurar).");
        Append(document, Settings, HttpMethod.Put,
            "Los plazos (272) y appName, senderName y emailLogoUrl (273) son opcionales: si faltan se conserva lo guardado. Fuera de su mínimo o su máximo, 400 de validación en su campo; emailLogoUrl va vacío o con https://.");
    }

    private static void DescribeSchemas(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { } schemas) return;
        Require(schemas, "RoleResponse", "grantable");
        // La auditoría de Plataforma sigue siendo una página por cursor: como mucho, el límite del cursor.
        if (Property(schemas, "PlatformAuditListResponse", "items") is { } page) page.MaxItems = CursorRequest.MaxLimit;
        foreach (var (schema, property, minimum, maximum) in new[]
        {
            ("UpdatePlatformSettingsHttpRequest", "loginAuditRetentionMonths", PlatformSettingsLimits.MinLoginAuditRetentionMonths, PlatformSettingsLimits.MaxLoginAuditRetentionMonths),
            ("UpdatePlatformSettingsHttpRequest", "loginCodeRetentionDays", PlatformSettingsLimits.MinLoginCodeRetentionDays, PlatformSettingsLimits.MaxLoginCodeRetentionDays),
            ("UpdatePlatformSettingsHttpRequest", "closedInvitationRetentionDays", PlatformSettingsLimits.MinClosedInvitationRetentionDays, PlatformSettingsLimits.MaxClosedInvitationRetentionDays),
        })
            if (Property(schemas, schema, property) is { } range)
            {
                // Valores del contrato, no texto para una persona: igual que ValidationContractContributor.
                range.Minimum = Convert.ToString(minimum, System.Globalization.CultureInfo.InvariantCulture);
                range.Maximum = Convert.ToString(maximum, System.Globalization.CultureInfo.InvariantCulture);
            }
        foreach (var property in new[] { "appName", "senderName" })
            if (Property(schemas, "UpdatePlatformSettingsHttpRequest", property) is { } name) name.MaxLength = PlatformSettingsLimits.NameMaxLength;
        if (Property(schemas, "UpdatePlatformSettingsHttpRequest", "emailLogoUrl") is { } logo)
            logo.Description = "Vacío o una dirección https://; vacío quita el logo.";
        foreach (var schema in new[] { "OrganizationUserHttpResponse", "CompanyMemberHttpResponse", "PlatformAccountResponse", "PlatformAccountDetailResponse" })
            if (Property(schemas, schema, "invitationDelivery") is { } delivery) delivery.Description = Delivery;
        if (Property(schemas, "TimeZoneReferenceHttpResponse", "countryTimeName") is { } countryTime)
            countryTime.Description = "La hora de su país en la cultura pedida («hora de Uruguay», 262); null en una zona sin país.";
    }

    private static void Require(IDictionary<string, IOpenApiSchema> schemas, string schema, string property)
    {
        if (!schemas.TryGetValue(schema, out var value) || value is not OpenApiSchema found) return;
        found.Required ??= new HashSet<string>(StringComparer.Ordinal);
        found.Required.Add(property);
    }

    private static OpenApiSchema? Property(IDictionary<string, IOpenApiSchema> schemas, string schema, string property) =>
        schemas.TryGetValue(schema, out var value) && value is OpenApiSchema found && found.Properties?.TryGetValue(property, out var member) == true
            ? member as OpenApiSchema : null;

    private static void Describe(OpenApiDocument document, string path, HttpMethod method, string text,
        IReadOnlyList<HttpContractError> errors, bool authenticated = false)
    {
        if (authenticated)
        {
            HttpContractDocumentation.Describe(document, path, method, text,
                [.. errors, E(401, "Http.Unauthorized"), E(403, "Http.Forbidden"), E(500, "General.Unexpected")], authenticated: true);
            return;
        }
        Append(document, path, method, text);
        HttpContractDocumentation.AddErrors(document, path, method, errors);
    }

    private static void Append(OpenApiDocument document, string path, HttpMethod method, string text)
    {
        if (HttpContractDocumentation.Find(document, path, method) is { } operation)
            operation.Description = string.IsNullOrEmpty(operation.Description) ? text : operation.Description + " " + text;
    }

    private static HttpContractError E(int status, string code) => HttpContractDocumentation.CoreError(status, code);
}
