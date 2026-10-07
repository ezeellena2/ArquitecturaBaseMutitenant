using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Recorre el registro por correo y obtiene una sesión consumer real para tests de cuenta.
/// Lee el código del pickup y completa el intercambio de autorización sin simular la API.
/// </summary>
internal sealed record AccountJourney(Guid UserId, Email Email)
{
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
    private static readonly ConditionalWeakTable<HttpClient, RegisteredAccount> Registrations = new();

    internal static async Task<AccountJourney> RegisterAsync(WebApplicationFactory<Program> factory, HttpClient client, CancellationToken ct, Email? destination = null)
    {
        await factory.Services.SeedDatabaseAsync(ct);
        var email = destination ?? Email.Create("account-journey-" + Guid.NewGuid().ToString("N") + "@example.test").Value;
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
        var account = new AccountJourney(profile!.RootElement.GetProperty("id").GetGuid(), email);
        Registrations.Remove(client);
        Registrations.Add(client, new RegisteredAccount(factory, email));
        return account;
    }

    internal static async Task AuthorizeAsync(HttpClient client, string access, CancellationToken ct, Guid? tenantId = null)
    {
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));
        var authorizePath = "/connect/authorize?client_id=web&response_type=code"
            + "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback&scope=openid%20profile%20email%20api"
            + "&code_challenge=" + challenge + "&code_challenge_method=S256&access=" + access
            + (tenantId is { } id ? "&tenant_id=" + id.ToString("D") : string.Empty);
        using var authorized = await AuthorizeResponseAsync(client, authorizePath, access, ct);
        Assert.True(authorized.StatusCode == HttpStatusCode.Redirect,
            $"Expected an authorization redirect, got {(int)authorized.StatusCode}: {await authorized.Content.ReadAsStringAsync(ct)}");
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

    private static async Task<HttpResponseMessage> AuthorizeResponseAsync(HttpClient client, string path, string access, CancellationToken ct)
    {
        var response = await client.GetAsync(path, ct);
        if (access != "platform" || response.Headers.Location?.OriginalString.StartsWith("/login?returnUrl=", StringComparison.Ordinal) != true
            || !Registrations.TryGetValue(client, out var registered)) return response;
        response.Dispose();

        // Los fixtures que convierten una persona en operador también ingresan por la puerta real de Plataforma.
        // Su código de registro consumido pertenece a la preparación, anterior a la ventana del nuevo ingreso.
        await using (var scope = registered.Factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async inner =>
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var codes = await context.LoginCodes.Where(code => code.Destination == registered.Email.Value
                    && code.ConsumedAtUtc != null).ToListAsync(inner);
                var setupAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddHours(-1);
                foreach (var code in codes) context.Entry(code).Property(item => item.CreatedAtUtc).CurrentValue = setupAtUtc;
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        using var requested = await PostAsync(client, "/api/auth/login-code", new { email = registered.Email.Value }, ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var value = await PickupCodeReader.ReadAsync(registered.Factory.Services, registered.Email.Value, ct);
        using var verified = await PostAsync(client, "/api/auth/login-code/verify",
            new { email = registered.Email.Value, code = value, returnUrl = path }, ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        return await client.GetAsync(path, ct);
    }

    internal static Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string path, T body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return SendAsync(client, request, ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        using (request) return await client.SendAsync(request, ct);
    }

    public override string ToString() => nameof(AccountJourney);
    private sealed record RegisteredAccount(WebApplicationFactory<Program> Factory, Email Email);
}
