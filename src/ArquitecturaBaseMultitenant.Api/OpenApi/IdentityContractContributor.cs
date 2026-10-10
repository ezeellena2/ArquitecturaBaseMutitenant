using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>Contrato de identidad E8, aditivo respecto de correo y separado de la implementación del transporte.</summary>
internal sealed class IdentityContractContributor : IHttpContractContributor
{
    private const string Destination = "Exactamente uno de email/phone. Sin channel se conserva correo; email exige channel=email o ausente. Phone exige canal registrado de tipo Phone. País ISO desde referencias; número se interpreta como celular y se normaliza a E.164.";
    private const string Proof = "Código como string de dígitos ASCII, longitud Authentication:LoginCode (6 por defecto). Intentos y topes actuales; retryAfter son segundos y coincide con Retry-After. No guardar códigos ni enlaces en logs.";
    private static readonly string[] SecretProperties = ["token", "nonce"];
    private static readonly HashSet<string> DestinationRequests = ["RequestLoginCodeHttpRequest", "VerifyLoginCodeHttpRequest", "SignupHttpRequest", "VerifySignupHttpRequest", "AddLoginMethodHttpRequest", "RequestRecoveryCodeHttpRequest", "VerifyRecoveryCodeHttpRequest", "SubmitAccountRecoveryHttpRequest"];
    private static readonly HashSet<string> CodeRequests = ["VerifyLoginCodeHttpRequest", "VerifySignupHttpRequest", "VerifyRecoveryCodeHttpRequest", "VerifyLoginMethodHttpRequest"];
    private static readonly HashSet<string> SecretRequests = ["PreviewLoginLinkHttpRequest", "RedeemLoginLinkHttpRequest", "RequestRecoveryCodeHttpRequest", "VerifyRecoveryCodeHttpRequest", "SubmitAccountRecoveryHttpRequest"];

    public void Describe(OpenApiDocument document)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "Token OIDC del acceso firmado consumer, business o platform. Mi cuenta no exige permiso de organización.",
        };
        document.Components.SecuritySchemes["PublicCookie"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = ".AspNetCore.Identity.Application",
            Description = "Cookie pública HttpOnly de identidad probada; no es un token OIDC ni elige empresa/persona.",
        };
        document.Components.SecuritySchemes["Antiforgery"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "RequestVerificationToken",
            Description = "Prueba antiforgery vigente de alta empresarial, junto con su cookie.",
        };
        DescribeSchemas(document);
        Describe(document, "/api/auth/methods", HttpMethod.Get,
            "Canales registrados disponibles, capacidades globales independientes de features por organización. ContactPhone opcional es número público E.164, nunca ID técnico.", []);
        Describe(document, "/api/auth/login-code", HttpMethod.Post, Destination + " Solo método verificado utilizable; conserva invitación por correo de ADR0047.",
            [E(400, "Auth.LoginCode.AccountNotFound", "email"), E(400, "Auth.LoginCode.PhoneAccountNotFound", "phone"), .. SendErrors()], true, examples: Destinations());
        Describe(document, "/api/auth/login-code/verify", HttpMethod.Post, Destination + Proof + " returnUrl OIDC o /invitacion conserva el flujo anterior y exige access ausente/null en el cuerpo. Continuación fija exclusivamente / con access=consumer o /org con access=business, valores literales sin query/fragmento; cualquier discrepancia falla antes de consumir el código. Respuesta returnUrl y cookie de identidad después del commit, sin tokens OIDC en body; el SPA inicia una autorización nueva tras el retorno fijo.",
            VerifyErrors(), true, examples: LoginCodeExamples());
        foreach (var prefix in new[] { "/api/auth/signup", "/api/auth/business-signup" })
        {
            var business = prefix.EndsWith("business-signup", StringComparison.Ordinal);
            var policy = business ? "BusinessSignup abierto, independiente de ConsumerSignup; identidad nueva o existente. No crea organización ni PhoneTaken personal."
                : "ConsumerSignup abierto; acepta términos explícitos. Crea Persona, nunca empresa.";
            var errors = SendErrors().Concat(new[] { E(403, business ? "Auth.BusinessSignup.Closed" : "Auth.Signup.Closed") }).ToList();
            if (!business) errors.AddRange([E(400, "Auth.Signup.EmailTaken", "email"), E(400, "Auth.Signup.PhoneTaken", "phone")]);
            Describe(document, business ? prefix + "/code" : prefix, HttpMethod.Post, Destination + policy, errors, true, examples: Destinations(terms: true));
            Describe(document, prefix + "/verify", HttpMethod.Post, Destination + Proof + policy
                + " Cuenta, método, Personal cuando corresponde y legales explícitos atómicos. Operador probado: PlatformOnly y sin cookie pública.",
                VerifyErrors().Concat(errors).DistinctBy(error => error.Code).ToArray(), true, examples: Destinations(code: true, terms: true));
        }
        Describe(document, "/api/me/login-methods", HttpMethod.Get,
            "Identidad propia consumer/business/platform. DTO vigente, Phone solo utilizable con canal disponible. Sin permiso de organización, sin ReauthTicket.", [], authenticated: true);
        Describe(document, "/api/me/login-methods", HttpMethod.Post, Destination + " Pendiente propio y OTP al destino nuevo; otro dueño falla antes de pendiente, aviso o OTP.",
            [E(400, "Identity.LoginMethod.AlreadyUsed", "email"), E(400, "Identity.LoginMethod.PhoneAlreadyUsed", "phone"), .. SendErrors()], true, true, Destinations());
        Describe(document, "/api/me/login-methods/{methodId}/code", HttpMethod.Post,
            "Reenvío para pendiente propio; recarga propiedad y disponibilidad, sin body. No prueba posesión del método principal.",
            [E(404, "Identity.LoginMethod.NotFound"), E(409, "Identity.LoginMethod.AlreadyVerified"), .. SendErrors()], true, true);
        Describe(document, "/api/me/login-methods/{methodId}/verify", HttpMethod.Post,
            Proof + " Solo OTP del pendiente propio; vínculo, principal si corresponde y avisos en una UoW.",
            [E(404, "Identity.LoginMethod.NotFound"), E(400, "Identity.LoginMethod.PhoneAlreadyUsed", "phone"), .. VerifyErrors()], true, true,
            new Dictionary<string, JsonNode> { ["codigo"] = new JsonObject { ["code"] = "123456" } });
        foreach (var (path, verb) in new[] { ("/api/me/login-methods/{methodId}/primary", HttpMethod.Put), ("/api/me/login-methods/{methodId}", HttpMethod.Delete) })
            Describe(document, path, verb, "Sin body ni código de confirmación (ADR0045). Relee propiedad y métodos utilizables bajo lock, conserva principal/personal y avisos. No usa version de Tus datos.",
                [E(404, "Identity.LoginMethod.NotFound"), E(409, "Identity.LoginMethod.LastMethod"), E(409, "Identity.LoginMethod.LastPersonalMethod"), E(409, "Auth.LoginMethod.NotVerified"), E(409, "General.ConcurrencyConflict")], authenticated: true);
        Describe(document, "/api/account-recovery/preview", HttpMethod.Post,
            "Exactamente previousEmail o previousPhone; método perdido no exige posesión. 204 cuando existe cuenta recuperable; sin datos de cuenta ni sesión.",
            [E(400, "Validation.Failed"), E(400, "Platform.AccountRecovery.AccountNotFound", "previousEmail"), E(400, "Platform.AccountRecovery.PhoneAccountNotFound", "previousPhone")],
            examples: new Dictionary<string, JsonNode> { ["correo"] = new JsonObject { ["previousEmail"] = "antes@example.com" }, ["telefono"] = new JsonObject { ["previousPhone"] = Phone() } });
        foreach (var (path, verify) in new[] { ("/api/account-recovery/code", false), ("/api/account-recovery/verify", true), ("/api/account-recovery", false) })
            Describe(document, path, HttpMethod.Post, Destination + Proof
                + " Nonce opaco base64url de 43 caracteres, ligado a este pedido. Sin sesión. Nuevo destino no se agrega a la cuenta. Submit: 202 sin body, prueba previa vigente y previousEmail o previousPhone; resolución asistida de Plataforma conserva reason/version/permiso/SecurityEvent.",
                [E(400, "Platform.AccountRecovery.PhoneMethodInUse", "phone"), E(400, "Platform.AccountRecovery.MethodInUse", "email"), E(400, "Platform.AccountRecovery.VerificationRequired"), .. (verify ? VerifyErrors() : SendErrors())], true,
                examples: Destinations(code: verify, nonce: true, previous: path == "/api/account-recovery"));
        foreach (var (path, redeem) in new[] { ("/api/auth/login-link/preview", false), ("/api/auth/login-link/redeem", true) })
            Describe(document, path, HttpMethod.Post,
                "Enlace opaco 256 bits (43 base64url), hash, 10 minutos, un uso y ligado a MethodId Phone verificado. Uno nuevo invalida los previos de ese método. Revalidar dueño, método, canal, estado y operador bajo lock; operador actual devuelve Invalid incluso en replay, sin cookie, datos o motivo revelador. Token via /login/enlace#t=… y solo memoria; jamás query/storage/log. "
                + (redeem ? "Solo access=consumer. 200 {access:consumer} y cookie pública, sin tokens OIDC. Replay misma key no crea otra sesión; otra key ya usado. Front ejecuta después signinRedirect con su state y PKCE."
                    : "Preview no consume ni crea cookie. displayName y maskedPhone nullable."),
                [E(400, "Validation.Failed"), E(400, "Auth.LoginLink.Invalid"), E(400, "Auth.LoginLink.Expired"), E(400, "Auth.LoginLink.AlreadyUsed"), E(429, "Auth.LoginLink.TooManyRequests")], redeem,
                examples: new Dictionary<string, JsonNode> { ["enlace"] = redeem ? new JsonObject { ["token"] = ExampleToken, ["access"] = "consumer" } : new JsonObject { ["token"] = ExampleToken } });
        Describe(document, "/api/auth/business-signup", HttpMethod.Post,
            "Contrato vigente de Tu organización. Requiere cookie pública probada y antiforgery; no recibe phone ni selecciona consumer. Valida BusinessSignup y límite/estado de organización; 200 BusinessSignupResponse existente.",
            [E(400, "Validation.Failed"), E(401, "Auth.BusinessSignup.IdentityRequired"), E(403, "Auth.BusinessSignup.Closed"), E(403, "Tenancy.Access.PlatformOnly")], true);
        document.Paths["/api/auth/business-signup"].Operations![HttpMethod.Post].Security = [new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("PublicCookie", document)] = [],
            [new OpenApiSecuritySchemeReference("Antiforgery", document)] = [],
        }];
        foreach (var action in new[] { "approve", "reject" })
            Describe(document, "/api/platform/recoveries/{id}/" + action, HttpMethod.Post,
                "Access platform y permiso " + ArquitecturaBaseMultitenant.Domain.Authorization.PlatformPermissions.Recoveries.Manage
                + " vigente. Body reason/version existente, decisión humana con SecurityEvent; conserva VersionResponse y concurrencia. Phone es un tipo de método, nunca se convierte en Email.",
                [E(400, "Validation.Failed"), E(404, "Platform.AccountRecovery.NotFound"), E(409, "Platform.AccountRecovery.AlreadyResolved"), E(409, "Platform.AccountRecovery.NoAccount"), E(409, "Platform.AccountRecovery.MethodInUse"), E(400, "Platform.AccountRecovery.PhoneMethodInUse"), E(409, "Platform.AccountRecovery.OperatorAccount"), E(409, "General.ConcurrencyConflict")], true, true);
        foreach (var path in new[] { "/api/auth/login-code/verify", "/api/auth/business-signup/verify", "/api/auth/login-link/redeem" })
        {
            var operation = document.Paths[path].Operations![HttpMethod.Post];
            var status = path.EndsWith("signup/verify", StringComparison.Ordinal) ? "204" : "200";
            if (operation.Responses?[status] is OpenApiResponse success)
            {
                success.Headers ??= new Dictionary<string, IOpenApiHeader>();
                success.Headers["Set-Cookie"] = new OpenApiHeader
                {
                    Description = "Cookie HttpOnly tras prueba válida; nunca se emite desde webhook ni desde preview.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                };
            }
        }
    }

    private const string ExampleToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static HttpContractError E(int status, string code, string? field = null) => HttpContractDocumentation.CoreError(status, code, field);
    private static HttpContractError[] SendErrors() => [E(400, "Validation.Failed"), E(400, "Users.Phone.Invalid", "phone"), E(400, "Users.Phone.NotMobile", "phone"), E(400, "Auth.LoginMethod.ChannelUnavailable", "channel"), E(429, "Auth.LoginCode.ResendTooSoon"), E(429, "Auth.LoginCode.TooManyRequests")];
    private static HttpContractError[] VerifyErrors() => [E(400, "Validation.Failed"), E(400, "Auth.LoginCode.Invalid", "code"), E(400, "Auth.LoginCode.Expired", "code"), E(400, "Auth.LoginCode.AlreadyUsed", "code"), E(400, "Auth.LoginCode.TooManyAttempts", "code"), E(403, "Auth.Account.Disabled"), E(403, "Auth.Account.LockedOut"), E(403, "Tenancy.Access.PlatformOnly"), E(400, "Auth.LoginMethod.ChannelUnavailable", "channel")];

    private static void Describe(OpenApiDocument document, string path, HttpMethod verb, string text, IReadOnlyList<HttpContractError> errors,
        bool idempotent = false, bool authenticated = false, IReadOnlyDictionary<string, JsonNode>? examples = null)
    {
        var common = new List<HttpContractError> { E(429, "Http.TooManyRequests"), E(500, "General.Unexpected") };
        if (authenticated) common.AddRange([E(401, "Http.Unauthorized"), E(403, "Http.Forbidden")]);
        if (idempotent) common.AddRange([E(400, "Request.IdempotencyKeyRequired"), E(409, "Request.IdempotencyKeyReused"), E(409, "Request.InProgress")]);
        HttpContractDocumentation.Describe(document, path, verb, text, errors.Concat(common).DistinctBy(error => error.Code).ToArray(), idempotent, authenticated, examples);
    }

    private static JsonObject Phone() => (JsonObject)Examples()["phone"]!.DeepClone();
    private static Dictionary<string, JsonNode> LoginCodeExamples()
    {
        var examples = Destinations(code: true, returnUrl: true);
        examples["personaFija"] = new JsonObject { ["email"] = "persona@example.com", ["code"] = "123456", ["returnUrl"] = "/", ["access"] = "consumer" };
        examples["empresaFija"] = new JsonObject { ["phone"] = Phone(), ["channel"] = "<phone-channel-key>", ["code"] = "123456", ["returnUrl"] = "/org", ["access"] = "business" };
        return examples;
    }
    private static Dictionary<string, JsonNode> Destinations(bool code = false, bool terms = false, bool returnUrl = false, bool nonce = false, bool previous = false)
    {
        var email = new JsonObject { ["email"] = "persona@example.com" };
        var phone = new JsonObject { ["channel"] = "<phone-channel-key>", ["phone"] = Phone() };
        foreach (var value in new[] { email, phone })
        {
            if (code) value["code"] = "123456";
            if (terms) { value["acceptedTerms"] = true; value["culture"] = Examples()["culture"]!.DeepClone(); value["timeZoneId"] = Examples()["timeZoneId"]!.DeepClone(); }
            if (returnUrl) value["returnUrl"] = "/connect/authorize?client_id=web&access=consumer&response_type=code&code_challenge=example";
            if (nonce) value["nonce"] = ExampleToken;
            if (previous) value["previousEmail"] = "antes@example.com";
        }
        return new Dictionary<string, JsonNode> { ["correo"] = email, ["telefono"] = phone };
    }

    private static JsonObject Examples()
    {
        using var stream = typeof(IdentityContractContributor).Assembly.GetManifestResourceStream("Api.OpenApi.IdentityExamples.json")
            ?? throw new InvalidOperationException("Missing embedded identity contract examples.");
        return JsonNode.Parse(stream)!.AsObject();
    }

    private static void DescribeSchemas(OpenApiDocument document)
    {
        foreach (var (name, value) in document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>())
        {
            if (value is not OpenApiSchema schema || schema.Properties is null) continue;
            if (DestinationRequests.Contains(name))
            {
                schema.Description = Destination;
                schema.OneOf = [ContactShape(document, "email", "phone"), ContactShape(document, "phone", "email")];
            }
            if (name is "PreviewAccountRecoveryHttpRequest" or "SubmitAccountRecoveryHttpRequest")
            {
                schema.AllOf ??= [];
                schema.AllOf.Add(new OpenApiSchema { OneOf = [ContactShape(document, "previousEmail", "previousPhone"), ContactShape(document, "previousPhone", "previousEmail")] });
            }
            foreach (var property in SecretProperties.Where(property => SecretRequests.Contains(name) && schema.Properties.ContainsKey(property)))
                schema.Properties[property] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Pattern = "^[A-Za-z0-9_-]{43}$", Description = "Secreto de 32 bytes, base64url sin padding. Solo cuerpo/memoria; sin logs." };
            if (CodeRequests.Contains(name)) schema.Properties["code"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null, Pattern = "^[0-9]+$", Description = "Dígitos ASCII; longitud configurada en Authentication:LoginCode (6 por defecto). No normalizar ni registrar.",
            };
            if (name == "VerifyLoginCodeHttpRequest") schema.Properties["access"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = "Opcional: consumer exacto sólo con returnUrl=/; business exacto sólo con returnUrl=/org. Para OIDC o /invitacion debe omitirse o ser null. Se conserva literal, sin normalizar.",
            };
            if (name == "RedeemLoginLinkHttpRequest" || name == "LoginLinkRedeemResponse") schema.Properties["access"] = new OpenApiSchema
            {
                Type = name == "LoginLinkRedeemResponse" ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null,
                Enum = [JsonValue.Create("consumer")!],
            };
            if (name == "LoginChannelAvailability") schema.Properties["contactPhone"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null, Pattern = "^\\+[1-9][0-9]{7,14}$", Description = "Número público E.164 aportado por el canal. Opcional; no es identificador del proveedor.",
            };
        }
    }

    private static OpenApiSchema ContactShape(OpenApiDocument document, string chosen, string other) => new()
    {
        Type = JsonSchemaType.Object, Required = new HashSet<string>(StringComparer.Ordinal) { chosen },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            [chosen] = chosen.EndsWith("Email", StringComparison.Ordinal) || chosen == "email"
                ? new OpenApiSchema { Type = JsonSchemaType.String }
                : new OpenApiSchemaReference("PhoneInputHttpRequest", document),
        },
        Not = new OpenApiSchema
        {
            Required = new HashSet<string>(StringComparer.Ordinal) { other },
            Properties = new Dictionary<string, IOpenApiSchema> { [other] = new OpenApiSchema { Type = other.EndsWith("Email", StringComparison.Ordinal) || other == "email" ? JsonSchemaType.String : JsonSchemaType.Object } },
        },
    };
}
