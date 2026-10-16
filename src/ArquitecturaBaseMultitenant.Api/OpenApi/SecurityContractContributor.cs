using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>
/// Contrato E9 (endurecimiento): segundo factor y reautenticación de Plataforma, restablecer el autenticador y las
/// cuotas compartidas de la configuración. El 429 de cada ruta y la prueba reciente los aplica <see cref="SharedContractRules"/>.
/// </summary>
internal sealed class SecurityContractContributor : IHttpContractContributor
{
    private const string Factor = "/api/auth/platform-factor";
    private const string Reauthentication = "/api/platform/reauthentication";
    private const string Reset = "/api/platform/accounts/{id}/authenticator/reset";
    private const string Settings = "/api/platform/settings";

    private const string Challenge = "Exige la cookie protegida del desafío pendiente (HttpOnly, host-only y ligada al navegador), que nace al probar correo o Google con access=platform y no autentica ninguna otra ruta; host principal y, en escrituras, origen válido. El desafío dura 10 minutos sin renovarse y no hay cookie de Plataforma ni tokens OIDC hasta completar el segundo factor. Respuestas con Cache-Control: no-store; ningún secreto va a la URL, a los logs ni al almacenamiento del navegador.";
    private const string Code = "Código: seis dígitos del autenticador (TOTP de 30 segundos con un intervalo de tolerancia por lado; un intervalo ya usado no vuelve a servir) o un código de recuperación XXXX-XXXX de un solo uso. No se registra.";
    private const string Lockout = "Comparte el contador de fallos del ingreso: diez fallos bloquean la cuenta 15 minutos, también con códigos de recuperación y en el alta; el bloqueo informa retryAfter y Retry-After y no acepta más códigos. Probar el primer factor no borra los fallos del segundo.";

    public void Describe(OpenApiDocument document)
    {
        DescribeSchemas(document);
        DescribeFactor(document);
        DescribeReauthentication(document);
        DescribeReset(document);
        DescribeIngress(document);
        DescribeSettings(document);
    }

    private static void DescribeFactor(OpenApiDocument document)
    {
        Describe(document, Factor, HttpMethod.Get, Challenge
            + " Devuelve el paso actual sin escribir: verify trae el vencimiento; setup, la clave y el otpauth preparados al probar el primer factor, para que el navegador arme el QR sin servicios externos; recoveryCodes, los ocho códigos del alta hasta completarla. Recargar la página consulta este estado.",
            [E(401, TwoFactorErrors.ChallengeExpiredCode)]);
        Describe(document, Factor + "/verify", HttpMethod.Post, Challenge + " " + Code
            + " Con el factor configurado acepta authenticator o recoveryCode y responde next=authorize con el retorno validado y la sesión de Plataforma creada después del commit. En el alta acepta sólo authenticator: verifica la clave y responde next=recoveryCodes, sin sesión; los códigos se leen con GET. Consumo del intervalo o del código, binding y auditoría van juntos; un replay revalida cuenta, sello, acceso y prueba antes de reconstruir la respuesta. " + Lockout,
            [E(400, ValidationError.ErrorCode), E(400, TwoFactorErrors.InvalidCode, TwoFactorErrors.CodeField),
                E(400, TwoFactorErrors.RecoveryInvalidCode, TwoFactorErrors.CodeField), E(401, TwoFactorErrors.ChallengeExpiredCode),
                E(409, TwoFactorErrors.InvalidStateCode), E(429, AccountErrors.LockedOutCode)], idempotent: true);
        Describe(document, Factor + "/complete", HttpMethod.Post, Challenge
            + " Sólo después de verificar el alta y con savedRecoveryCodes=true: guarda los hashes de los códigos, crea la sesión de Plataforma, acepta la invitación de operador si estaba pendiente y destruye la clave temporal y los códigos en claro del desafío. Responde el retorno validado del ingreso.",
            [E(400, ValidationError.ErrorCode), E(401, TwoFactorErrors.ChallengeExpiredCode), E(409, TwoFactorErrors.InvalidStateCode)], idempotent: true);
        foreach (var (path, method) in new[] { (Factor, HttpMethod.Get), (Factor + "/verify", HttpMethod.Post), (Factor + "/complete", HttpMethod.Post) })
            NoStore(document, path, method);
        foreach (var path in new[] { Factor + "/verify", Factor + "/complete" })
            SuccessHeader(document, path, HttpMethod.Post, "Set-Cookie",
                "Cookie de Plataforma HttpOnly, host-only e independiente de la pública (ADR 0044), sólo cuando el segundo factor queda completo; también retira la cookie del desafío.");
        LockoutWithRetryAfter(document, Factor + "/verify");
    }

    private static void DescribeReauthentication(OpenApiDocument document)
    {
        Describe(document, Reauthentication, HttpMethod.Post,
            "Access platform y operador activo, sin otro permiso. " + Code
            + " La prueba dura 10 minutos y queda ligada a esta sesión de Plataforma y a su sello de seguridad: otra pestaña de la misma sesión la comparte, otra sesión no; cerrar o revocar sesiones, quitar el operador o restablecer su factor la invalidan. verifiedAtUtc y recentUntilUtc son del servidor; ninguna fecha del cliente prueba antigüedad. Un replay conserva el vencimiento original. " + Lockout,
            [E(400, ValidationError.ErrorCode), E(400, TwoFactorErrors.InvalidCode, TwoFactorErrors.CodeField),
                E(400, TwoFactorErrors.RecoveryInvalidCode, TwoFactorErrors.CodeField), E(403, AccessErrors.WrongCode),
                E(429, AccountErrors.LockedOutCode)], idempotent: true, authenticated: true);
        NoStore(document, Reauthentication, HttpMethod.Post);
        LockoutWithRetryAfter(document, Reauthentication);
    }

    private static void DescribeReset(OpenApiDocument document) =>
        Describe(document, Reset, HttpMethod.Post,
            "Access platform, permiso platform.operators.manage y prueba reciente; la falta de permiso se resuelve antes de pedir el código. version es el operatorVersion de la ficha de la cuenta y reason usa los límites del motivo de las acciones actuales. Bajo lock verifica la versión, que el destinatario sea un operador aceptado y distinto del autor y las restricciones de PlatformOperatorGuard. Revoca sesiones y autorizaciones, desafío, clave y códigos del destinatario, conserva sus permisos y su próximo ingreso exige el alta; audita autor, destino y motivo en la misma UoW y responde la nueva versión de operador.",
            [E(400, ValidationError.ErrorCode), E(403, AccessErrors.WrongCode), E(403, UserErrors.CannotModifySelfCode),
                E(404, UserErrors.NotFoundCode), E(404, PlatformOperatorErrors.NotOperatorCode),
                E(409, PlatformOperatorErrors.InvitationPendingCode), E(409, PlatformOperatorErrors.LastManagerCode),
                E(409, "General.ConcurrencyConflict")], idempotent: true, authenticated: true);

    private static void DescribeIngress(OpenApiDocument document)
    {
        Append(document, "/api/auth/login-code/verify", HttpMethod.Post,
            "next=authorize conserva la conducta anterior. next=platformFactor (access=platform, E9): el código quedó probado pero no hay cookie de Plataforma ni tokens; nace la cookie del desafío pendiente y el SPA lo consulta en /api/auth/platform-factor. Una invitación de operador queda aceptada recién al completar ese ingreso.");
        Append(document, "/api/auth/external/callback", HttpMethod.Get,
            "Operador (E9): después de probar Google redirige al ingreso con factor=pending, sin identidad ni prueba en la URL; el indicador sólo hace que el SPA consulte el desafío pendiente. Sin cookie de Plataforma ni tokens hasta completar el segundo factor.");
        Append(document, "/api/auth/login-link/redeem", HttpMethod.Post,
            "Un operador nunca recibe sesión por enlace: sigue devolviendo Invalid (E9).");
    }

    private static void DescribeSettings(OpenApiDocument document)
    {
        Append(document, Settings, HttpMethod.Get,
            "rateLimits (E9): los doce perfiles cerrados de cuotas compartidas, con ventanas fijas UTC y sin cola.");
        Append(document, Settings, HttpMethod.Put,
            "rateLimits (E9) es opcional: omitirlo conserva las cuotas guardadas y enviarlo exige los doce perfiles completos, permitLimit entre 1 y 1.000.000 y windowSeconds entre 1 y 86.400, sin cero para apagar una cuota. Conserva version, reason, la respuesta {version} y el permiso; además pide prueba reciente.");
        HttpContractDocumentation.AddErrors(document, Settings, HttpMethod.Put, [E(400, ValidationError.ErrorCode)]);
    }

    private static void DescribeSchemas(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { } schemas) return;
        if (Schema(schemas, "LoginCodeNextStep") is { } next)
            next.Description = "authorize: abrir returnUrl como antes. platformFactor: operador con el primer factor probado; consultar /api/auth/platform-factor.";
        foreach (var name in new[] { "TwoFactorMethod", "PlatformFactorState", "PlatformFactorNextStep", "LoginCodeNextStep" })
            if (Schema(schemas, name) is { } enumeration) NotNull(enumeration);

        schemas["PlatformFactorVerifyChallenge"] = Variant("verify", new());
        schemas["PlatformFactorSetupChallenge"] = Variant("setup", new()
        {
            ["secret"] = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^[A-Z2-7]+$", Description = "Clave base32 para copiar a mano." },
            ["otpauthUri"] = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^otpauth://totp/", Description = "Contenido del QR que arma el navegador." },
        });
        schemas["PlatformFactorRecoveryCodesChallenge"] = Variant("recoveryCodes", new()
        {
            ["recoveryCodes"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array, MinItems = PlatformFactorChallengeResponse.RecoveryCodeCount,
                MaxItems = PlatformFactorChallengeResponse.RecoveryCodeCount, UniqueItems = true,
                Items = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^[A-Z0-9]{4}-[A-Z0-9]{4}$" },
                Description = "Los ocho códigos de un solo uso; copiar y descargar son acciones locales del navegador.",
            },
        });
        Union(document, schemas, "PlatformFactorChallengeResponse", "state", new()
        {
            ["verify"] = "PlatformFactorVerifyChallenge", ["setup"] = "PlatformFactorSetupChallenge",
            ["recoveryCodes"] = "PlatformFactorRecoveryCodesChallenge",
        }, "El paso del desafío pendiente de Plataforma (E9).");

        schemas["VerifyPlatformFactorAuthorizeResponse"] = Closed(new()
        {
            ["next"] = Literal("authorize"), ["returnUrl"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Retorno validado del ingreso; el SPA abre el authorize." },
        });
        schemas["VerifyPlatformFactorRecoveryCodesResponse"] = Closed(new() { ["next"] = Literal("recoveryCodes") });
        Union(document, schemas, "VerifyPlatformFactorResponse", "next", new()
        {
            ["authorize"] = "VerifyPlatformFactorAuthorizeResponse", ["recoveryCodes"] = "VerifyPlatformFactorRecoveryCodesResponse",
        }, "authorize con la sesión creada o recoveryCodes al verificar el alta (E9).");

        foreach (var name in new[] { "VerifyPlatformFactorHttpRequest", "PlatformReauthenticationHttpRequest" })
            if (Schema(schemas, name) is { } request)
            {
                request.AdditionalPropertiesAllowed = false;
                request.Required = new HashSet<string>(StringComparer.Ordinal) { "method", "code" };
                request.Properties!["method"] = new OpenApiSchemaReference("TwoFactorMethod", document);
                request.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 6, MaxLength = 9, Description = Code };
            }
        if (Schema(schemas, "CompletePlatformFactorHttpRequest") is { } complete)
        {
            complete.AdditionalPropertiesAllowed = false;
            complete.Required = new HashSet<string>(StringComparer.Ordinal) { "savedRecoveryCodes" };
            complete.Properties!["savedRecoveryCodes"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Boolean, Enum = [JsonValue.Create(true)], Description = "La casilla «Guardé los códigos»; sólo vale true.",
            };
        }
        if (Schema(schemas, "PlatformReauthenticationResponse") is { } recent) Close(recent);

        foreach (var name in new[] { "PlatformRateLimitsResponse", "PlatformRateLimitsInput" })
            if (Schema(schemas, name) is { } limits)
            {
                Close(limits);
                var quota = name.EndsWith("Input", StringComparison.Ordinal) ? "RateLimitQuotaInput" : "RateLimitQuotaResponse";
                foreach (var key in limits.Properties!.Keys.ToArray()) limits.Properties[key] = new OpenApiSchemaReference(quota, document);
                limits.Required = limits.Properties.Keys.ToHashSet(StringComparer.Ordinal);
                limits.Description = "Los doce perfiles cerrados (E9): ip, publicIp, identity, tenant, loginCode, loginVerify, invitationPreview, invitationAccept, assistantQuickQuery, assistantConversation, assistantNewKey y whatsAppWebhook.";
            }
        foreach (var name in new[] { "RateLimitQuotaResponse", "RateLimitQuotaInput" })
            if (Schema(schemas, name) is { } quota)
            {
                Close(quota);
                quota.Required = new HashSet<string>(StringComparer.Ordinal) { "permitLimit", "windowSeconds" };
                quota.Properties!["permitLimit"] = Range(RateLimitQuota.MinPermitLimit, RateLimitQuota.MaxPermitLimit, "Pedidos admitidos por ventana.");
                quota.Properties["windowSeconds"] = Range(RateLimitQuota.MinWindowSeconds, RateLimitQuota.MaxWindowSeconds, "Duración de la ventana fija UTC, en segundos.");
            }
    }

    private static OpenApiSchema Variant(string state, Dictionary<string, IOpenApiSchema> extra)
    {
        var properties = new Dictionary<string, IOpenApiSchema>
        {
            ["state"] = Literal(state),
            ["expiresAtUtc"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time", Description = "Vencimiento del desafío, sin renovación." },
        };
        foreach (var (key, value) in extra) properties[key] = value;
        return Closed(properties);
    }

    private static void Union(OpenApiDocument document, IDictionary<string, IOpenApiSchema> schemas, string name, string property,
        Dictionary<string, string> variants, string description) =>
        schemas[name] = new OpenApiSchema
        {
            Description = description,
            OneOf = variants.Values.Select(value => (IOpenApiSchema)new OpenApiSchemaReference(value, document)).ToList(),
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = property,
                Mapping = variants.ToDictionary(pair => pair.Key, pair => new OpenApiSchemaReference(pair.Value, document)),
            },
        };

    private static OpenApiSchema Closed(Dictionary<string, IOpenApiSchema> properties) => new()
    {
        Type = JsonSchemaType.Object, AdditionalPropertiesAllowed = false, Properties = properties,
        Required = properties.Keys.ToHashSet(StringComparer.Ordinal),
    };

    private static void Close(OpenApiSchema schema)
    {
        schema.Type = JsonSchemaType.Object;
        schema.AdditionalPropertiesAllowed = false;
    }

    private static OpenApiSchema Literal(string value) => new() { Type = JsonSchemaType.String, Enum = [JsonValue.Create(value)!] };

    private static OpenApiSchema Range(int minimum, int maximum, string description) => new()
    {
        Type = JsonSchemaType.Integer, Format = "int32", Description = description,
        // Valores del contrato, no texto para una persona: igual que ValidationContractContributor.
        Minimum = Convert.ToString(minimum, System.Globalization.CultureInfo.InvariantCulture),
        Maximum = Convert.ToString(maximum, System.Globalization.CultureInfo.InvariantCulture),
    };

    private static void NotNull(OpenApiSchema schema)
    {
        schema.Type = JsonSchemaType.String;
        schema.Enum = schema.Enum?.Where(value => value is not null).ToList();
    }

    private static OpenApiSchema? Schema(IDictionary<string, IOpenApiSchema> schemas, string name) =>
        schemas.TryGetValue(name, out var schema) ? schema as OpenApiSchema : null;

    private static void Describe(OpenApiDocument document, string path, HttpMethod method, string text,
        IReadOnlyList<HttpContractError> errors, bool idempotent = false, bool authenticated = false)
    {
        var common = new List<HttpContractError> { E(500, "General.Unexpected") };
        if (authenticated) common.AddRange([E(401, "Http.Unauthorized"), E(403, "Http.Forbidden")]);
        if (idempotent) common.AddRange([E(400, "Request.IdempotencyKeyRequired"), E(422, "Request.IdempotencyKeyReused"), E(409, "Request.InProgress")]);
        HttpContractDocumentation.Describe(document, path, method, text, errors.Concat(common).DistinctBy(error => error.Code).ToArray(), idempotent, authenticated);
    }

    private static void Append(OpenApiDocument document, string path, HttpMethod method, string text)
    {
        if (HttpContractDocumentation.Find(document, path, method) is { } operation)
            operation.Description = string.IsNullOrEmpty(operation.Description) ? text : operation.Description + " " + text;
    }

    private static void NoStore(OpenApiDocument document, string path, HttpMethod method) =>
        SuccessHeader(document, path, method, "Cache-Control", "no-store: el navegador no guarda la respuesta.");

    private static void SuccessHeader(OpenApiDocument document, string path, HttpMethod method, string name, string description)
    {
        if (HttpContractDocumentation.Find(document, path, method)?.Responses?.TryGetValue("200", out var response) != true
            || response is not OpenApiResponse success) return;
        success.Headers ??= new Dictionary<string, IOpenApiHeader>();
        success.Headers[name] = new OpenApiHeader { Description = description, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
    }

    /// <summary>En el segundo factor el bloqueo sí informa el plazo, a diferencia del primer factor.</summary>
    private static void LockoutWithRetryAfter(OpenApiDocument document, string path)
    {
        if (HttpContractDocumentation.Find(document, path, HttpMethod.Post)?.Responses?.TryGetValue("429", out var response) != true
            || response is not OpenApiResponse limited || limited.Content?.TryGetValue("application/problem+json", out var media) != true
            || media is null) return;
        foreach (var culture in new[] { "es", "en" })
            if (media.Examples?.TryGetValue(AccountErrors.LockedOutCode + "." + culture, out var example) == true
                && example is OpenApiExample { Value: JsonObject value })
                value["retryAfter"] = 900;
        limited.Headers ??= new Dictionary<string, IOpenApiHeader>();
        limited.Headers["Retry-After"] = new OpenApiHeader
        {
            Description = "Segundos enteros iguales a retryAfter, también para Identity.Account.LockedOut en esta ruta.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
        };
    }

    private static HttpContractError E(int status, string code, string? field = null) => HttpContractDocumentation.CoreError(status, code, field);
}
