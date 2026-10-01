using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.WebUtilities;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Recorre el registro por correo y obtiene una sesión consumer real para tests de cuenta.
/// Lee el código del pickup y completa el intercambio de autorización sin simular la API.
/// </summary>
internal sealed record AccountJourney(Guid UserId, Email Email)
{
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

    internal static async Task<AccountJourney> RegisterAsync(ApiFactory factory, HttpClient client, CancellationToken ct)
    {
        await factory.Services.SeedDatabaseAsync(ct);
        var email = Email.Create("account-journey-" + Guid.NewGuid().ToString("N") + "@example.test").Value;
        using var requested = await PostAsync(client, "/api/auth/signup",
            new { email = email.Value, acceptedTerms = true, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires" }, ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email.Value, ct);
        using var verified = await PostAsync(client, "/api/auth/signup/verify",
            new { email = email.Value, code, acceptedTerms = true, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires" }, ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        await AuthorizeAsync(client, "consumer", ct);
        using var me = await client.GetAsync("/api/me", ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var profile = await me.Content.ReadFromJsonAsync<JsonDocument>(ct);
        return new AccountJourney(profile!.RootElement.GetProperty("id").GetGuid(), email);
    }

    internal static async Task AuthorizeAsync(HttpClient client, string access, CancellationToken ct)
    {
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));
        using var authorized = await client.GetAsync("/connect/authorize?client_id=web&response_type=code"
            + "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback&scope=openid%20profile%20email%20api"
            + "&code_challenge=" + challenge + "&code_challenge_method=S256&access=" + access, ct);
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);
        var authorizationCode = QueryHelpers.ParseQuery(authorized.Headers.Location!.Query)["code"].ToString();
        using var exchanged = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = authorizationCode, ["client_id"] = "web",
            ["redirect_uri"] = "https://localhost:5174/auth/callback", ["code_verifier"] = Verifier,
        }), ct);
        Assert.Equal(HttpStatusCode.OK, exchanged.StatusCode);
        using var tokens = await exchanged.Content.ReadFromJsonAsync<JsonDocument>(ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens!.RootElement.GetProperty("access_token").GetString());
    }

    internal static Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string path, T body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return SendAsync(client, request, ct);
    }

    internal static async Task<string> IssueReauthTicketAsync(IServiceProvider services, Guid userId,
        ReauthAction action, CancellationToken ct)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var source = await context.LoginMethods.AsNoTracking().FirstAsync(method =>
            method.UserId == userId && method.VerifiedAtUtc != null, ct);
        var secrets = services.GetRequiredService<ISecureTokenGenerator>();
        var secret = secrets.Generate();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(token =>
        {
            context.ReauthTickets.Add(ReauthTicket.Issue(userId, action, source.Id, null, secrets.Hash(secret),
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, ct);
        return secret;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        using (request) return await client.SendAsync(request, ct);
    }

    public override string ToString() => nameof(AccountJourney);
}
