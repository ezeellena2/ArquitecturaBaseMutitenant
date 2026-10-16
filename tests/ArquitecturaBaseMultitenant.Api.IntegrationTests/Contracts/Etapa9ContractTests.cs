using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

/// <summary>
/// Contrato E9 publicado antes de implementarlo (K1): segundo factor y reautenticación de Plataforma, restablecer el
/// autenticador, prueba reciente en las escrituras sensibles, cuotas de la configuración y el 429 de cada ruta.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class Etapa9ContractTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Escrituras sensibles del núcleo, cerradas: una nueva se suma a esta lista a propósito (las de módulos salen de su metadata).
    private static readonly string[] CoreSensitiveWrites =
    [
        "post /api/platform/operators/invitations", "post /api/platform/operators/{id}/invitation/resend",
        "delete /api/platform/operators/{id}/invitation", "put /api/platform/accounts/{id}/operator",
        "delete /api/platform/accounts/{id}/operator", "post /api/platform/accounts/{id}/authenticator/reset",
        "post /api/platform/accounts/{id}/suspend", "post /api/platform/accounts/{id}/reactivate",
        "post /api/platform/accounts/{id}/revoke-sessions", "post /api/platform/accounts/{id}/deletion",
        "post /api/platform/recoveries/{id}/approve", "post /api/platform/recoveries/{id}/reject",
        "post /api/platform/tenants", "post /api/platform/tenants/{id}/approve", "post /api/platform/tenants/{id}/retry-provisioning",
        "post /api/platform/tenants/{id}/reject", "post /api/platform/tenants/{id}/suspend", "post /api/platform/tenants/{id}/reactivate",
        "post /api/platform/tenants/{id}/close", "put /api/platform/tenants/{id}/features/{featureKey}",
        "post /api/platform/tenants/{id}/domains/{domain}/verify", "delete /api/platform/tenants/{id}/domains/{domain}",
        "post /api/platform/tenants/{id}/public-site/unpublish", "post /api/platform/tenants/{id}/public-site/allow-publish",
        "post /api/platform/legal-documents", "put /api/platform/settings",
    ];

    private static readonly string[] RateLimitProfiles =
    [
        "ip", "publicIp", "identity", "tenant", "loginCode", "loginVerify", "invitationPreview", "invitationAccept",
        "assistantQuickQuery", "assistantConversation", "assistantNewKey", "whatsAppWebhook",
    ];

    [Fact]
    public void Sensitive_platform_writes_require_recent_proof_and_reads_attention_and_use_do_not()
    {
        var operations = Operations(Contract()).ToArray();
        var sensitive = operations.Where(value => value.Operation["x-recent-authentication"]?.GetValue<bool>() == true)
            .Select(value => value.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Empty(CoreSensitiveWrites.Except(sensitive, StringComparer.Ordinal));
        foreach (var (key, _, operation) in operations)
        {
            var proof = Codes(operation).Contains("403 Auth.Platform.ReauthenticationRequired");
            Assert.True(proof == sensitive.Contains(key), $"{key}: the 403 and x-recent-authentication must go together.");
            if (!proof) continue;
            Assert.False(key.StartsWith("get ", StringComparison.Ordinal), key);
            Assert.StartsWith("/api/platform/", key[(key.IndexOf(' ', StringComparison.Ordinal) + 1)..], StringComparison.Ordinal);
            var permissions = operation["x-permissions"]?.AsArray().Select(value => value!.GetValue<string>()).ToArray() ?? [];
            if (!CoreSensitiveWrites.Contains(key))
                Assert.Contains(permissions, permission => PlatformPermissions.RequiringRecentProof.Contains(permission));
        }
        foreach (var (key, _, _) in operations.Where(value => value.Operation["x-permissions"]?.AsArray()
            .Any(permission => permission!.GetValue<string>() is PlatformPermissions.WhatsApp.Attend or PlatformPermissions.Assistant.Use) == true))
            Assert.DoesNotContain(key, sensitive);
        Assert.DoesNotContain("post /api/platform/reauthentication", sensitive);
    }

    [Fact]
    public void Every_api_route_can_answer_the_shared_quota_with_retry_after()
    {
        var document = Contract();
        foreach (var (key, _, operation) in Operations(document).Where(value => value.Path.StartsWith("/api/", StringComparison.Ordinal)))
        {
            Assert.Contains("429 Security.RateLimit.Exceeded", Codes(operation));
            Assert.DoesNotContain("429 Http.TooManyRequests", Codes(operation));
            var limited = operation["responses"]!["429"]!;
            Assert.True(60 == limited["content"]!["application/problem+json"]!["examples"]!["Security.RateLimit.Exceeded.es"]!["value"]!["retryAfter"]!.GetValue<int>(), key);
            Assert.NotNull(limited["headers"]?["Retry-After"]);
        }
        // El webhook de Meta queda afuera de /api y conserva su propio código.
        if (document["paths"]?["/webhooks/whatsapp"]?["post"] is { } webhook)
            Assert.DoesNotContain("429 Security.RateLimit.Exceeded", Codes(webhook));
    }

    [Fact]
    public void Second_factor_and_reauthentication_publish_closed_shapes_statuses_and_errors()
    {
        var document = Contract();
        var schemas = document["components"]!["schemas"]!;
        var challenge = schemas["PlatformFactorChallengeResponse"]!;
        Assert.Equal(["recoveryCodes", "setup", "verify"], Keys(challenge["discriminator"]!["mapping"]!));
        Assert.Equal(3, challenge["oneOf"]!.AsArray().Count);
        Assert.Equal(["expiresAtUtc", "otpauthUri", "secret", "state"], Strings(schemas["PlatformFactorSetupChallenge"]!["required"]!));
        var codes = schemas["PlatformFactorRecoveryCodesChallenge"]!["properties"]!["recoveryCodes"]!;
        Assert.Equal(8, codes["minItems"]!.GetValue<int>());
        Assert.Equal(8, codes["maxItems"]!.GetValue<int>());
        Assert.Equal("^[A-Z0-9]{4}-[A-Z0-9]{4}$", codes["items"]!["pattern"]!.GetValue<string>());
        foreach (var name in new[] { "PlatformFactorVerifyChallenge", "PlatformFactorSetupChallenge", "PlatformFactorRecoveryCodesChallenge", "VerifyPlatformFactorAuthorizeResponse", "VerifyPlatformFactorRecoveryCodesResponse" })
            Assert.False(schemas[name]!["additionalProperties"]!.GetValue<bool>(), name);
        Assert.Equal(["authorize", "recoveryCodes"], Keys(schemas["VerifyPlatformFactorResponse"]!["discriminator"]!["mapping"]!));
        Assert.Equal(["authenticator", "recoveryCode"], Strings(schemas["TwoFactorMethod"]!["enum"]!));
        foreach (var name in new[] { "VerifyPlatformFactorHttpRequest", "PlatformReauthenticationHttpRequest" })
            Assert.Equal(["code", "method"], Strings(schemas[name]!["required"]!));
        Assert.True(schemas["CompletePlatformFactorHttpRequest"]!["properties"]!["savedRecoveryCodes"]!["enum"]![0]!.GetValue<bool>());
        Assert.Equal(["recentUntilUtc", "verifiedAtUtc"], Strings(schemas["PlatformReauthenticationResponse"]!["required"]!));

        var paths = document["paths"]!;
        AssertStatuses(paths["/api/auth/platform-factor"]!["get"]!, 200, 401);
        AssertStatuses(paths["/api/auth/platform-factor/verify"]!["post"]!, 200, 400, 401, 409, 429);
        AssertStatuses(paths["/api/auth/platform-factor/complete"]!["post"]!, 200, 400, 401, 409);
        AssertStatuses(paths["/api/platform/reauthentication"]!["post"]!, 200, 400, 401, 403, 429);
        AssertStatuses(paths["/api/platform/accounts/{id}/authenticator/reset"]!["post"]!, 200, 400, 401, 403, 404, 409, 429);
        foreach (var path in new[] { "/api/auth/platform-factor/verify", "/api/platform/reauthentication" })
        {
            var operation = paths[path]!["post"]!;
            var invalid = Assert.Single(operation["x-error-codes"]!.AsArray(), value => value!["code"]!.GetValue<string>() == "Auth.TwoFactor.Invalid");
            Assert.Equal("code", invalid!["field"]!.GetValue<string>());
            Assert.Equal("El código no es válido. Revisá que la hora de tu celular esté bien.", invalid["es"]!.GetValue<string>());
            Assert.Equal(900, operation["responses"]!["429"]!["content"]!["application/problem+json"]!["examples"]!["Identity.Account.LockedOut.es"]!["value"]!["retryAfter"]!.GetValue<int>());
            Assert.NotNull(operation["responses"]!["200"]!["headers"]!["Cache-Control"]);
            Assert.Contains(operation["parameters"]!.AsArray(), parameter => parameter!["name"]!.GetValue<string>() == "Idempotency-Key");
        }
        Assert.NotNull(paths["/api/auth/platform-factor"]!["get"]!["responses"]!["200"]!["headers"]!["Cache-Control"]);
        Assert.Contains("409 General.ConcurrencyConflict", Codes(paths["/api/platform/accounts/{id}/authenticator/reset"]!["post"]!));
        Assert.Contains("403 Users.User.CannotModifySelf", Codes(paths["/api/platform/accounts/{id}/authenticator/reset"]!["post"]!));
    }

    [Fact]
    public void Settings_and_login_code_publish_rate_limits_and_the_next_step()
    {
        var schemas = Contract()["components"]!["schemas"]!;
        Assert.Contains("rateLimits", Strings(schemas["PlatformSettingsResponse"]!["required"]!));
        Assert.DoesNotContain("rateLimits", Strings(schemas["UpdatePlatformSettingsHttpRequest"]!["required"] ?? new JsonArray()));
        foreach (var name in new[] { "PlatformRateLimitsResponse", "PlatformRateLimitsInput" })
            Assert.Equal(RateLimitProfiles.Order(StringComparer.Ordinal), Strings(schemas[name]!["required"]!));
        var quota = schemas["RateLimitQuotaInput"]!["properties"]!;
        Assert.Equal("1", quota["permitLimit"]!["minimum"]!.ToString());
        Assert.Equal("1000000", quota["permitLimit"]!["maximum"]!.ToString());
        Assert.Equal("86400", quota["windowSeconds"]!["maximum"]!.ToString());
        Assert.Contains("next", Strings(schemas["VerifyLoginCodeResponse"]!["required"]!));
        Assert.Equal(["authorize", "platformFactor"], Strings(schemas["LoginCodeNextStep"]!["enum"]!));
    }

    [Fact]
    public async Task Second_factor_stubs_answer_501_without_cookies()
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using (var read = await client.GetAsync("/api/auth/platform-factor", Ct)) await AssertStubAsync(read);
        foreach (var (path, body) in new (string, object)[]
            {
                ("/api/auth/platform-factor/verify", new { method = "authenticator", code = "123456" }),
                ("/api/auth/platform-factor/complete", new { savedRecoveryCodes = true }),
            })
        {
            using var response = await SendAsync(client, path, body);
            await AssertStubAsync(response);
        }
    }

    [Fact]
    public async Task Reauthentication_and_reset_keep_access_and_permission_ahead_of_their_stubs()
    {
        using var anonymous = factory.CreateClient(new() { AllowAutoRedirect = false });
        using (var denied = await SendAsync(anonymous, "/api/platform/reauthentication", new { method = "authenticator", code = "123456" }))
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var reduced = await OperatorAsync(PlatformOperators.Reduced);
        using (var proof = await SendAsync(reduced, "/api/platform/reauthentication", new { method = "recoveryCode", code = "ABCD-1234" }))
            await AssertStubAsync(proof);
        var target = Guid.CreateVersion7();
        using (var forbidden = await SendAsync(reduced, $"/api/platform/accounts/{target:D}/authenticator/reset", new { version = 1, reason = "Perdió el celular" }))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var manager = await OperatorAsync(PlatformOperators.All);
        using (var reset = await SendAsync(manager, $"/api/platform/accounts/{target:D}/authenticator/reset", new { version = 1, reason = "Perdió el celular" }))
            await AssertStubAsync(reset);
        using var settings = await manager.GetAsync("/api/platform/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        var limits = (await settings.Content.ReadFromJsonAsync<JsonObject>(Ct))!["rateLimits"]!;
        Assert.Equal(RateLimitProfiles.Order(StringComparer.Ordinal), Keys(limits));
        Assert.Equal(300, limits["ip"]!["permitLimit"]!.GetValue<int>());
        Assert.Equal(900, limits["assistantNewKey"]!["windowSeconds"]!.GetValue<int>());
    }

    private async Task<HttpClient> OperatorAsync(IReadOnlyCollection<string> permissions)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        return await client.SendAsync(request, Ct);
    }

    private static async Task AssertStubAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal("General.Contract.NotImplemented", problem?["code"]?.GetValue<string>());
        Assert.NotNull(problem?["traceId"]);
    }

    private static void AssertStatuses(JsonNode operation, params int[] statuses)
    {
        var declared = Keys(operation["responses"]!);
        foreach (var status in statuses) Assert.Contains(status.ToString(System.Globalization.CultureInfo.InvariantCulture), declared);
    }

    private static IEnumerable<(string Key, string Path, JsonNode Operation)> Operations(JsonNode document) =>
        document["paths"]!.AsObject().SelectMany(path => path.Value!.AsObject()
            .Where(method => method.Value?["responses"] is not null)
            .Select(method => ($"{method.Key} {path.Key}", path.Key, method.Value!)));

    private static string[] Codes(JsonNode operation) =>
        operation["x-error-codes"]?.AsArray().Select(value => value!["status"]!.GetValue<int>().ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " " + value["code"]!.GetValue<string>()).ToArray() ?? [];

    private static string[] Keys(JsonNode value) => [.. value.AsObject().Select(pair => pair.Key).Order(StringComparer.Ordinal)];

    private static string[] Strings(JsonNode value) => [.. value.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal)];

    private static JsonNode Contract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
        return JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "contracts", "openapi.json")))!;
    }
}
