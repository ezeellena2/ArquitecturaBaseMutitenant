using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

/// <summary>Contrato E8: rutas exportables, formas congeladas y rechazo sin sesiones cuando falta una prueba o canal.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class Etapa8ContractTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly object PhoneBody = new { phone = new { country = "AR", number = "11 2345 6789" }, channel = "whatsapp", acceptedTerms = true, nonce = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" };

    [Theory]
    [InlineData("/api/account-recovery/code")]
    public async Task Phone_recovery_without_an_available_channel_never_creates_a_cookie(string path)
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(PhoneBody) };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal("Auth.LoginMethod.ChannelUnavailable", problem?["code"]?.GetValue<string>());
        Assert.NotNull(problem?["traceId"]);
    }

    [Theory]
    [InlineData("/api/auth/login-code")]
    [InlineData("/api/auth/signup")]
    [InlineData("/api/auth/business-signup/code")]
    public async Task Phone_auth_without_an_available_channel_never_creates_a_cookie(string path)
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(PhoneBody) };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal("Auth.LoginMethod.ChannelUnavailable", problem?["code"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("redeem")]
    public async Task Unknown_login_link_never_creates_a_session(string action)
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-link/" + action)
        {
            Content = JsonContent.Create(new { token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", access = "consumer" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Email_and_phone_together_are_rejected_before_sending()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-code")
        {
            Content = JsonContent.Create(new { email = "persona@example.com", phone = new { country = "AR", number = "11 2345 6789" }, channel = "whatsapp" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal("Validation.Failed", problem?["code"]?.GetValue<string>());
        Assert.NotNull(problem?["errors"]?["phone"]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Technical_webhook_requires_its_registered_global_availability(bool installed, bool enabled)
    {
        await using var application = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInstalledModuleRouteAvailability>();
            if (installed) services.AddSingleton<IInstalledModuleRouteAvailability>(new Availability(enabled));
        }));
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=example&hub.challenge=example-challenge", Ct);
        Assert.Equal(enabled ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal(enabled ? "WhatsApp.Webhook.InvalidSignature" : "Http.NotFound", problem?["code"]?.GetValue<string>());
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task OpenApi_freezes_contact_shapes_consumer_links_and_bilingual_errors()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseDevelopmentTestOwner());
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", Ct);
        var doc = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schemas = doc!["components"]!["schemas"]!;
        Assert.Equal(2, schemas["RequestLoginCodeHttpRequest"]!["oneOf"]!.AsArray().Count);
        Assert.Equal("^[A-Za-z0-9_-]{43}$", schemas["RedeemLoginLinkHttpRequest"]!["properties"]!["token"]!["pattern"]!.GetValue<string>());
        Assert.Equal("consumer", schemas["LoginLinkRedeemResponse"]!["properties"]!["access"]!["enum"]![0]!.GetValue<string>());
        Assert.NotNull(schemas["LoginChannelAvailability"]!["properties"]!["contactPhone"]);
        Assert.NotNull(schemas["ProblemDetails"]!["properties"]!["attemptsLeft"]);
        Assert.Equal("string", schemas["PermissionItem"]!["properties"]!["code"]!["type"]!.GetValue<string>());
        Assert.Null(schemas["PublicPageHttpResponse"]?["oneOf"]);
        var errors = doc["paths"]!["/api/auth/login-code"]!["post"]!["x-error-codes"]!.AsArray();
        var phone = Assert.Single(errors, error => error?["code"]?.GetValue<string>() == "Auth.LoginCode.PhoneAccountNotFound");
        Assert.Equal("No hay una cuenta con ese número.", phone!["es"]!.GetValue<string>());
        Assert.Equal("There is no account with that number.", phone["en"]!.GetValue<string>());
        var email = Assert.Single(errors, error => error?["code"]?.GetValue<string>() == "Auth.LoginCode.AccountNotFound");
        Assert.Equal(400, email!["status"]!.GetValue<int>()); // FieldErrors conserva el 400 actual, aunque la fábrica base sea NotFound.
        Assert.NotNull(doc["paths"]!["/webhooks/whatsapp"]!["post"]!["responses"]!["413"]);
        Assert.DoesNotContain(doc["paths"]!["/webhooks/whatsapp"]!["post"]!["parameters"]!.AsArray(), value => value?["name"]?.GetValue<string>() == "Idempotency-Key");
        foreach (var (path, method) in OriginalE8Operations)
            {
                var operation = doc["paths"]![path]![method]!;
                Assert.Null(operation?["responses"]?["501"]);
                Assert.DoesNotContain("T01", operation?["description"]?.GetValue<string>() ?? "");
                Assert.DoesNotContain(operation?["x-error-codes"]?.AsArray() ?? [], value => value?["code"]?.GetValue<string>() == "General.Contract.NotImplemented");
            }
    }

    // Complete immutable E8 operation inventory: later contract-only routes cannot relax these checks.
    private static readonly (string Path, string Method)[] OriginalE8Operations =
    [
        ("/webhooks/whatsapp", "get"),
        ("/webhooks/whatsapp", "post"),
        ("/api/reference-data", "get"),
        ("/api/reference-data/currencies", "get"),
        ("/api/reference-data/countries", "get"),
        ("/api/reference-data/time-zones", "get"),
        ("/api/reference-data/cultures", "get"),
        ("/api/reference-data/tax-id-types", "get"),
        ("/api/directory", "get"),
        ("/api/directory/{slug}/logo", "get"),
        ("/api/public-site", "get"),
        ("/api/public-site/logo", "get"),
        ("/api/platform/accounts/paged", "get"),
        ("/api/platform/accounts/filter-counts", "get"),
        ("/api/platform/accounts/{id}", "get"),
        ("/api/platform/accounts/{id}/suspend", "post"),
        ("/api/platform/accounts/{id}/reactivate", "post"),
        ("/api/platform/accounts/{id}/revoke-sessions", "post"),
        ("/api/platform/accounts/{id}/deletion", "post"),
        ("/api/platform/audit", "get"),
        ("/api/platform/audit/organizations", "get"),
        ("/api/platform/audit/{id}", "get"),
        ("/api/platform/tenants/{id}/domains", "get"),
        ("/api/platform/tenants/{id}/domains/{domain}/verify", "post"),
        ("/api/platform/tenants/{id}/domains/{domain}", "delete"),
        ("/api/platform/tenants/{id}/features", "get"),
        ("/api/platform/tenants/{id}/features/{featureKey}", "put"),
        ("/api/platform/legal-documents", "get"),
        ("/api/platform/legal-documents", "post"),
        ("/api/platform/legal-documents/{id}", "get"),
        ("/api/platform/operators/invitations", "post"),
        ("/api/platform/operators/{id}/invitation/resend", "post"),
        ("/api/platform/operators/{id}/invitation", "delete"),
        ("/api/platform/accounts/{id}/operator", "put"),
        ("/api/platform/accounts/{id}/operator", "delete"),
        ("/api/platform/tenants/{id}/public-site", "get"),
        ("/api/platform/tenants/{id}/public-site/unpublish", "post"),
        ("/api/platform/tenants/{id}/public-site/allow-publish", "post"),
        ("/api/platform/recoveries/paged", "get"),
        ("/api/platform/recoveries/filter-counts", "get"),
        ("/api/platform/recoveries/{id}/approve", "post"),
        ("/api/platform/recoveries/{id}/reject", "post"),
        ("/api/platform/settings", "get"),
        ("/api/platform/settings", "put"),
        ("/api/platform/tenants", "post"),
        ("/api/platform/tenants/{id}/approve", "post"),
        ("/api/platform/tenants/{id}/retry-provisioning", "post"),
        ("/api/platform/tenants/paged", "get"),
        ("/api/platform/tenants/filter-counts", "get"),
        ("/api/platform/tenants/{id}", "get"),
        ("/api/platform/tenants/{id}/history", "get"),
        ("/api/platform/tenants/{id}/reject", "post"),
        ("/api/platform/tenants/{id}/suspend", "post"),
        ("/api/platform/tenants/{id}/reactivate", "post"),
        ("/api/platform/tenants/{id}/close", "post"),
        ("/api/companies/paged", "get"),
        ("/api/companies/filter-counts", "get"),
        ("/api/companies/{companyId}", "get"),
        ("/api/companies/{companyId}", "put"),
        ("/api/companies", "post"),
        ("/api/companies/{companyId}/status", "put"),
        ("/api/companies/{companyId}/candidates/paged", "get"),
        ("/api/companies/{companyId}/invitations/{invitationId}/resend", "post"),
        ("/api/companies/{companyId}/invitations/{invitationId}", "delete"),
        ("/api/companies/{companyId}/members/paged", "get"),
        ("/api/companies/{companyId}/members/filter-counts", "get"),
        ("/api/companies/{companyId}/members", "post"),
        ("/api/companies/{companyId}/members/{memberId}/roles", "put"),
        ("/api/companies/{companyId}/members/{memberId}/status", "put"),
        ("/api/companies/{companyId}/roles/paged", "get"),
        ("/api/home", "get"),
        ("/api/organization/audit", "get"),
        ("/api/organization/audit/actors", "get"),
        ("/api/organization/settings/domains", "get"),
        ("/api/organization/settings/domains", "post"),
        ("/api/organization/settings/domains/{domainId}/verify", "post"),
        ("/api/organization/settings/domains/{domainId}", "delete"),
        ("/api/organization/settings", "get"),
        ("/api/organization/settings", "put"),
        ("/api/users/paged", "get"),
        ("/api/users/filter-counts", "get"),
        ("/api/users/{memberId}", "get"),
        ("/api/users/{memberId}", "put"),
        ("/api/users/{memberId}", "delete"),
        ("/api/users/invitations", "post"),
        ("/api/users/invitations/{invitationId}/resend", "post"),
        ("/api/users/invitations/{invitationId}", "delete"),
        ("/api/users/{memberId}/status", "put"),
        ("/api/permissions", "get"),
        ("/api/organization/public-page", "get"),
        ("/api/organization/public-page", "put"),
        ("/api/organization/public-page/publish", "post"),
        ("/api/organization/public-page/unpublish", "post"),
        ("/api/roles/paged", "get"),
        ("/api/roles/{id}", "get"),
        ("/api/roles/{id}", "put"),
        ("/api/roles/{id}", "delete"),
        ("/api/roles", "post"),
        ("/api/auth/deletion/cancel", "post"),
        ("/api/auth/deletion/pending", "post"),
        ("/api/account-recovery/preview", "post"),
        ("/api/account-recovery/code", "post"),
        ("/api/account-recovery/verify", "post"),
        ("/api/account-recovery", "post"),
        ("/api/auth/business-signup/code", "post"),
        ("/api/auth/business-signup/verify", "post"),
        ("/api/auth/business-signup", "post"),
        ("/api/contact", "get"),
        ("/api/auth/external/google", "get"),
        ("/api/auth/external/google", "post"),
        ("/api/auth/external/google/antiforgery", "get"),
        ("/api/auth/external/callback", "get"),
        ("/api/invitations/preview", "post"),
        ("/api/invitations/accept", "post"),
        ("/api/auth/login-code", "post"),
        ("/api/auth/login-code/verify", "post"),
        ("/api/auth/login-link/preview", "post"),
        ("/api/auth/login-link/redeem", "post"),
        ("/api/auth/methods", "get"),
        ("/api/auth/signup", "post"),
        ("/api/auth/signup/verify", "post"),
        ("/api/public-site/slug-availability", "get"),
        ("/api/me/deletion", "post"),
        ("/api/me/external/google", "post"),
        ("/api/me/login-methods", "get"),
        ("/api/me/login-methods", "post"),
        ("/api/me/login-methods/{methodId}/code", "post"),
        ("/api/me/login-methods/{methodId}/verify", "post"),
        ("/api/me/login-methods/{methodId}/primary", "put"),
        ("/api/me/login-methods/{methodId}", "delete"),
        ("/api/legal/accept", "post"),
        ("/api/legal/terms", "get"),
        ("/api/legal/privacy", "get"),
        ("/api/me", "get"),
        ("/api/me", "put"),
    ];

    private sealed class Availability(bool available) : IInstalledModuleRouteAvailability
    {
        public string Key => "WhatsApp.Webhook";
        public bool IsAvailable => available;
    }
}
