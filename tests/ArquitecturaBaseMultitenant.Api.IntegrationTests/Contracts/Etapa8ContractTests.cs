using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

/// <summary>Contrato T01: rutas visibles/exportables, pendientes explícitos y cero sesiones por los esqueletos.</summary>
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
    public async Task Login_link_stub_never_creates_a_session(string action)
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-link/" + action)
        {
            Content = JsonContent.Create(new { token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", access = "consumer" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
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
        Assert.Equal(enabled ? HttpStatusCode.NotImplemented : HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal(enabled ? "General.Contract.NotImplemented" : "Http.NotFound", problem?["code"]?.GetValue<string>());
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
    }

    private sealed class Availability(bool available) : IInstalledModuleRouteAvailability
    {
        public string Key => "WhatsApp.Webhook";
        public bool IsAvailable => available;
    }
}
