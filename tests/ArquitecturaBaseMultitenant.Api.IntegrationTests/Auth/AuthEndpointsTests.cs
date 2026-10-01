using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba las puertas HTTP de ingreso y registro. Exige canales configurados, aceptación legal y estado
/// externo confiable antes de iniciar o completar Google.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AuthEndpointsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Public_methods_only_advertise_configured_channels()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/methods", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MethodsBody>(Ct);
        Assert.NotNull(body);
        Assert.Contains(body.Channels, channel => channel.Key == "email");
        Assert.DoesNotContain(body.Channels, channel => channel.Key == "google");
    }

    [Fact]
    public async Task Anonymous_signup_requires_current_terms_at_the_http_boundary()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/signup")
        {
            Content = JsonContent.Create(new { email = "new-person@example.test", acceptedTerms = false }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Ct);
        Assert.Equal("Validation.Failed", problem?.Code);
        Assert.Contains("acceptedTerms", Assert.IsType<Dictionary<string, string[]>>(problem?.Errors).Keys);
    }

    [Fact]
    public async Task Unconfigured_google_does_not_offer_a_challenge()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/external/google?access=consumer&returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dweb", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Google_callback_ignores_untrusted_signup_query_without_external_state()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(
            "/api/auth/external/callback?signup=true&acceptedTerms=true", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?error=Auth.ExternalLogin.Failed", response.Headers.Location?.ToString());
    }

    private sealed record MethodsBody(IReadOnlyList<ChannelBody> Channels);

    private sealed record ChannelBody(string Key);

    private sealed record ProblemBody(string Code, Dictionary<string, string[]>? Errors);
}
