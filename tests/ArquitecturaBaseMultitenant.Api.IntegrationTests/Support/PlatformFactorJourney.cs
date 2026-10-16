using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Completa el segundo factor de un operador (E9) por las rutas reales, después de probar el primero: el alta con el
/// código TOTP de la clave que muestra el desafío y los ocho códigos guardados, o el autenticador ya configurado. La
/// clave se lee del desafío o, para un autenticador existente, se describe con el mismo puerto que usa el alta.
/// </summary>
internal static class PlatformFactorJourney
{
    internal const string Path = "/api/auth/platform-factor";

    /// <summary>Devuelve el retorno validado del ingreso; la cookie de Plataforma ya quedó en el cliente.</summary>
    internal static async Task<string> CompleteAsync(IServiceProvider services, HttpClient client, Guid? userId,
        CancellationToken ct)
    {
        using var challenge = await ChallengeAsync(client, ct);
        var state = challenge.RootElement.GetProperty("state").GetString();
        if (state == "setup")
        {
            var secret = challenge.RootElement.GetProperty("secret").GetString()!;
            using (var verified = await PostAsync(services, client, Path + "/verify",
                new { method = "authenticator", code = Code(secret, services) }, ct))
            {
                Assert.True(verified.StatusCode == HttpStatusCode.OK, await verified.Content.ReadAsStringAsync(ct));
                using var next = JsonDocument.Parse(await verified.Content.ReadAsStringAsync(ct));
                Assert.Equal("recoveryCodes", next.RootElement.GetProperty("next").GetString());
            }
            using (var codes = await ChallengeAsync(client, ct))
                Assert.Equal(8, codes.RootElement.GetProperty("recoveryCodes").GetArrayLength());
            using var completed = await PostAsync(services, client, Path + "/complete", new { savedRecoveryCodes = true }, ct);
            Assert.True(completed.StatusCode == HttpStatusCode.OK, await completed.Content.ReadAsStringAsync(ct));
            using var body = JsonDocument.Parse(await completed.Content.ReadAsStringAsync(ct));
            return body.RootElement.GetProperty("returnUrl").GetString()!;
        }

        Assert.Equal("verify", state);
        Assert.True(userId is not null, "An operator with an authenticator needs its account id to prove it.");
        using var authorized = await PostAsync(services, client, Path + "/verify",
            new { method = "authenticator", code = await CurrentCodeAsync(services, userId!.Value, ct) }, ct);
        Assert.True(authorized.StatusCode == HttpStatusCode.OK, await authorized.Content.ReadAsStringAsync(ct));
        using var result = JsonDocument.Parse(await authorized.Content.ReadAsStringAsync(ct));
        Assert.Equal("authorize", result.RootElement.GetProperty("next").GetString());
        return result.RootElement.GetProperty("returnUrl").GetString()!;
    }

    /// <summary>
    /// Vuelve a ingresar a Plataforma desde cero, como un navegador: código por correo, segundo factor y authorize. Sirve
    /// después de cerrar sesiones o de volver a aplicar la migración del factor, que revoca las sesiones anteriores.
    /// </summary>
    internal static async Task SignInAsync(IServiceProvider services, HttpClient client, string email, Guid userId,
        CancellationToken ct)
    {
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes
                .Where(code => code.Destination == email)
                .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.CreatedAtUtc, code => code.CreatedAtUtc.AddHours(-1)), ct);
        using (var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email }, ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(services, email, ct);
        using (var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = "/connect/authorize?access=platform" }, ct))
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await CompleteAsync(services, client, userId, ct);
        await AccountJourney.AuthorizeAsync(client, "platform", ct);
    }

    /// <summary>
    /// El código del autenticador ya configurado. Dos ingresos de una prueba pueden caer en el mismo intervalo de 30
    /// segundos: se libera el último intervalo usado, como hace un reloj que avanzó.
    /// </summary>
    internal static async Task<string> CurrentCodeAsync(IServiceProvider services, Guid userId, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.PlatformAuthenticators.Where(authenticator => authenticator.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(authenticator => authenticator.LastAcceptedTimeStep, 0L), ct);
        var protectedSecret = await context.PlatformAuthenticators.Where(authenticator => authenticator.UserId == userId)
            .Select(authenticator => authenticator.ProtectedSecret).SingleAsync(ct);
        var secret = scope.ServiceProvider.GetRequiredService<IPlatformFactorSecrets>()
            .DescribeSetup(userId, protectedSecret, "test").Secret;
        return Code(secret, services);
    }

    internal static Task<HttpResponseMessage> PostAsync(IServiceProvider services, HttpClient client, string path,
        object body, CancellationToken ct, Guid? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (idempotencyKey ?? Guid.NewGuid()).ToString("D"));
        // Las escrituras del desafío exigen el origen público, como las manda el navegador.
        if (services.GetRequiredService<IPublicOrigin>().Value is { } origin)
            request.Headers.Add("Origin", origin.GetLeftPart(UriPartial.Authority));
        return SendAsync(client, request, ct);
    }

    /// <summary>El TOTP del intervalo actual para una clave base32 (RFC 6238).</summary>
    internal static string Code(string base32Secret, IServiceProvider services, int offset = 0)
    {
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds() / 30;
        return PlatformFactorSecrets.Totp(Base32(base32Secret), now + offset);
    }

    internal static byte[] Base32(string value)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var character in value.TrimEnd('='))
        {
            buffer = ((buffer << 5) | Alphabet.IndexOf(character, StringComparison.Ordinal)) & 0xFFFF;
            bits += 5;
            if (bits < 8) continue;
            bytes.Add((byte)(buffer >> (bits - 8)));
            bits -= 8;
        }
        return [.. bytes];
    }

    private static async Task<JsonDocument> ChallengeAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(Path, ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request,
        CancellationToken ct)
    {
        using (request) return await client.SendAsync(request, ct);
    }
}
