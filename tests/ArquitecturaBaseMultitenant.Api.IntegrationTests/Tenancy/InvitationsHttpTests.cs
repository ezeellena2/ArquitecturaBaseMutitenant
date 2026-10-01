using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

[Collection(ApiTestGroup.Name)]
public sealed class InvitationsHttpTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preview_requires_antiforgery_and_keeps_the_token_in_the_json_body()
    {
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        const string token = "sensitive-invitation-token-probe";

        using var missingAntiforgery = await client.PostAsJsonAsync("/api/invitations/preview", new { token }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, missingAntiforgery.StatusCode);
        Assert.Equal("Validation.Failed", await ProblemCodeAsync(missingAntiforgery));

        var antiforgery = await client.GetFromJsonAsync<GoogleSignupAntiforgeryResponse>(
            "/api/auth/external/google/antiforgery", Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/invitations/preview")
        {
            Content = JsonContent.Create(new { token }),
        };
        request.Headers.Add("RequestVerificationToken", antiforgery!.RequestToken);
        using var preview = await client.SendAsync(request, Ct);
        var body = await preview.Content.ReadAsStringAsync(Ct);
        using var previewDocument = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("Invalid", previewDocument.RootElement.GetProperty("state").GetString());
        Assert.Equal("/api/invitations/preview", preview.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Null(preview.Headers.Location);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        var invitationCookie = Assert.Single(preview.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("__Host-MtInvitation=;", invitationCookie, StringComparison.Ordinal);
        Assert.DoesNotContain(token, invitationCookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Accept_requires_antiforgery_and_an_idempotency_key()
    {
        await using var limitedFactory = new ApiFactory();
        await limitedFactory.InitializeAsync();
        using var client = limitedFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var antiforgery = await client.GetFromJsonAsync<GoogleSignupAntiforgeryResponse>(
            "/api/auth/external/google/antiforgery", Ct);

        using var missingAntiforgery = CreateAcceptRequest(Guid.NewGuid().ToString("D"), null);
        using var csrfRejected = await client.SendAsync(missingAntiforgery, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, csrfRejected.StatusCode);
        Assert.Equal("Validation.Failed", await ProblemCodeAsync(csrfRejected));

        using var missingKey = CreateAcceptRequest(null, antiforgery!.RequestToken);
        using var keyRejected = await client.SendAsync(missingKey, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, keyRejected.StatusCode);
        Assert.Equal("Request.IdempotencyKeyRequired", await ProblemCodeAsync(keyRejected));

        var permitLimit = limitedFactory.Services.GetRequiredService<IConfiguration>()
            .GetValue<int>("RateLimiting:InvitationAcceptPermitLimit");
        Assert.True(permitLimit >= 2);
        for (var index = 0; index < permitLimit - 2; index++)
        {
            using var acceptedRequest = CreateAcceptRequest(Guid.NewGuid().ToString("D"), antiforgery.RequestToken);
            using var rejected = await client.SendAsync(acceptedRequest, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal("Invitations.Invitation.Invalid", await ProblemCodeAsync(rejected));
        }

        using var limitedRequest = CreateAcceptRequest(Guid.NewGuid().ToString("D"), antiforgery.RequestToken);
        using var limited = await client.SendAsync(limitedRequest, Ct);
        using var limitedDocument = await ProblemDocumentAsync(limited);

        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
        Assert.Equal("Http.TooManyRequests", limitedDocument.RootElement.GetProperty("code").GetString());
        var retryAfter = limitedDocument.RootElement.GetProperty("retryAfter").GetInt32();
        Assert.True(retryAfter > 0);
        Assert.Equal(retryAfter, int.Parse(limited.Headers.GetValues("Retry-After").Single(), CultureInfo.InvariantCulture));
    }

    private static HttpRequestMessage CreateAcceptRequest(string? idempotencyKey, string? antiforgeryToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/invitations/accept")
        {
            Content = JsonContent.Create(new { token = "invalid", acceptedTerms = true }),
        };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (antiforgeryToken is not null) request.Headers.Add("RequestVerificationToken", antiforgeryToken);
        return request;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = await ProblemDocumentAsync(response);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<JsonDocument> ProblemDocumentAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
}
