using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Segundo factor del operador (E9). Probar el código a Plataforma deja sólo un desafío ligado al navegador, sin
/// cookie de Plataforma ni tokens; el alta pide la clave nueva y los códigos guardados; un código de recuperación
/// sirve una vez y un intervalo del autenticador también; diez fallos bloquean la cuenta; reintentar la misma clave
/// después de perder la respuesta repite la misma sesión, y un authorize sin esa evidencia no entrega tokens.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformTwoFactorTests(ApiFactory factory)
{
    private const string PlatformReturn = "/connect/authorize?access=platform";
    private const string ChallengeCookie = "__Host-MtPlatformFactor";
    private const string PlatformCookie = ".AspNetCore.Identity.Platform";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_first_factor_leaves_a_pending_challenge_without_a_platform_session_or_tokens()
    {
        var (client, userId, email) = await OperatorAsync();
        var before = Now();

        using (var verified = await FirstFactorAsync(client, email))
        {
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            var body = await JsonAsync(verified);
            Assert.Equal("platformFactor", body.GetProperty("next").GetString());
            Assert.Equal(PlatformReturn, body.GetProperty("returnUrl").GetString());
            var cookies = SetCookies(verified);
            Assert.Contains(cookies, cookie => cookie.StartsWith(ChallengeCookie + "=", StringComparison.Ordinal)
                && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase)
                && cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(cookies, cookie => cookie.StartsWith(PlatformCookie + "=", StringComparison.Ordinal));
        }

        // Sin la cookie de Plataforma el authorize vuelve al ingreso, sin código ni tokens.
        using (var authorize = await client.GetAsync(AuthorizePath(), Ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
            Assert.StartsWith("/login?returnUrl=", authorize.Headers.Location!.OriginalString, StringComparison.Ordinal);
        }

        // El desafío dice el paso, sin escribir: la clave y el otpauth para armar el QR en el navegador.
        using var challenge = await client.GetAsync(PlatformFactorJourney.Path, Ct);
        Assert.Equal(HttpStatusCode.OK, challenge.StatusCode);
        Assert.True(challenge.Headers.CacheControl?.NoStore);
        var state = await JsonAsync(challenge);
        Assert.Equal("setup", state.GetProperty("state").GetString());
        Assert.Matches("^[A-Z2-7]{32}$", state.GetProperty("secret").GetString());
        Assert.StartsWith("otpauth://totp/", state.GetProperty("otpauthUri").GetString(), StringComparison.Ordinal);
        Assert.InRange(state.GetProperty("expiresAtUtc").GetDateTime(), Now(), Now().AddMinutes(10));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.PlatformSessions.AnyAsync(session => session.UserId == userId, Ct));
        Assert.False(await db.PlatformAuthenticators.AnyAsync(authenticator => authenticator.UserId == userId, Ct));
        // Probar el primer factor no registra el ingreso: se registra al completar los dos.
        Assert.False(await db.LoginAudits.AnyAsync(audit => audit.UserId == userId && audit.Succeeded
            && audit.OccurredAtUtc >= before, Ct));
    }

    [Fact]
    public async Task The_setup_needs_a_valid_code_and_the_saved_codes_before_the_session_exists()
    {
        var (client, userId, email) = await OperatorAsync();
        using (var verified = await FirstFactorAsync(client, email)) Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var secret = (await ChallengeAsync(client)).GetProperty("secret").GetString()!;

        // Fuera de secuencia: completar sin verificar, o el alta con un código de recuperación.
        await AssertErrorAsync(await PostAsync(client, "/complete", new { savedRecoveryCodes = true }), HttpStatusCode.Conflict, TwoFactorErrors.InvalidStateCode);
        await AssertErrorAsync(await PostAsync(client, "/verify", new { method = "recoveryCode", code = "ABCD-2345" }), HttpStatusCode.Conflict, TwoFactorErrors.InvalidStateCode);
        // El formato se valida sin gastar un intento; un código incorrecto sí cuenta.
        await AssertErrorAsync(await PostAsync(client, "/verify", new { method = "authenticator", code = "12ab56" }), HttpStatusCode.BadRequest, "Validation.Failed");
        Assert.Equal(0, await FailuresAsync(userId));
        var wrong = Wrong(PlatformFactorJourney.Code(secret, factory.Services));
        using (var invalid = await PostAsync(client, "/verify", new { method = "authenticator", code = wrong }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var problem = await JsonAsync(invalid);
            Assert.Equal(TwoFactorErrors.InvalidCode, problem.GetProperty("code").GetString());
            Assert.Equal("El código no es válido. Revisá que la hora de tu celular esté bien.",
                Assert.Single(problem.GetProperty("errors").GetProperty("code").EnumerateArray()).GetString());
        }
        Assert.Equal(1, await FailuresAsync(userId));

        using (var verified = await PostAsync(client, "/verify", new { method = "authenticator", code = PlatformFactorJourney.Code(secret, factory.Services) }))
        {
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            Assert.Equal("recoveryCodes", (await JsonAsync(verified)).GetProperty("next").GetString());
            Assert.DoesNotContain(SetCookies(verified), cookie => cookie.StartsWith(PlatformCookie + "=", StringComparison.Ordinal));
        }
        var codes = (await ChallengeAsync(client)).GetProperty("recoveryCodes").EnumerateArray().Select(code => code.GetString()!).ToArray();
        Assert.Equal(8, codes.Length);
        await AssertErrorAsync(await PostAsync(client, "/complete", new { savedRecoveryCodes = false }), HttpStatusCode.BadRequest, "Validation.Failed");

        using (var completed = await PostAsync(client, "/complete", new { savedRecoveryCodes = true }))
        {
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
            Assert.Equal(PlatformReturn, (await JsonAsync(completed)).GetProperty("returnUrl").GetString());
            var cookies = SetCookies(completed);
            Assert.Contains(cookies, cookie => cookie.StartsWith(PlatformCookie + "=", StringComparison.Ordinal));
            Assert.Contains(cookies, cookie => cookie.StartsWith(ChallengeCookie + "=;", StringComparison.Ordinal));
        }
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using (var me = await client.GetAsync("/api/me", Ct)) Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(8, await db.PlatformRecoveryCodes.CountAsync(code => code.UserId == userId, Ct));
        var stored = await db.PlatformRecoveryCodes.Where(code => code.UserId == userId).Select(code => code.CodeHash).ToArrayAsync(Ct);
        Assert.All(codes, code => Assert.DoesNotContain(code, stored));
        var challenge = await db.PlatformFactorChallenges.SingleAsync(item => item.UserId == userId && item.CompletedAtUtc != null, Ct);
        Assert.Null(challenge.ProtectedSecret);
        Assert.Null(challenge.ProtectedRecoveryCodes);
        Assert.True(await db.LoginAudits.AnyAsync(audit => audit.UserId == userId && audit.Succeeded
            && audit.Method == LoginAuditMethod.Authenticator, Ct));
        Assert.True(await db.SecurityEvents.AnyAsync(item => item.Type == SecurityEventType.PlatformAuthenticatorConfigured
            && item.ActorId == userId && item.TargetAccountId == userId, Ct));
        Assert.Equal(0, await FailuresAsync(userId));
    }

    [Fact]
    public async Task A_recovery_code_and_an_authenticator_interval_each_serve_once()
    {
        var (client, userId, email) = await OperatorAsync();
        using (var first = await FirstFactorAsync(client, email)) Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var secret = (await ChallengeAsync(client)).GetProperty("secret").GetString()!;
        var setupStep = CurrentStep();
        using (var verified = await PostAsync(client, "/verify", new { method = "authenticator", code = CodeAt(secret, setupStep) }))
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var recovery = (await ChallengeAsync(client)).GetProperty("recoveryCodes")[0].GetString()!;
        using (var completed = await PostAsync(client, "/complete", new { savedRecoveryCodes = true }))
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        // Otro navegador: el código de recuperación entra una vez, en minúsculas y sin guion también.
        using var second = Client();
        using (var verified = await FirstFactorAsync(second, email)) Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Equal("verify", (await ChallengeAsync(second)).GetProperty("state").GetString());
        using (var used = await PostAsync(second, "/verify", new { method = "recoveryCode", code = recovery.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant() }))
        {
            Assert.Equal(HttpStatusCode.OK, used.StatusCode);
            var body = await JsonAsync(used);
            Assert.Equal("authorize", body.GetProperty("next").GetString());
            Assert.Equal(PlatformReturn, body.GetProperty("returnUrl").GetString());
        }
        using var third = Client();
        using (var verified = await FirstFactorAsync(third, email)) Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AssertErrorAsync(await PostAsync(third, "/verify", new { method = "recoveryCode", code = recovery }),
            HttpStatusCode.BadRequest, TwoFactorErrors.RecoveryInvalidCode);

        // El intervalo que ya usó el alta tampoco vuelve a servir, aunque siga dentro de la tolerancia.
        await AssertErrorAsync(await PostAsync(third, "/verify", new { method = "authenticator", code = CodeAt(secret, setupStep) }),
            HttpStatusCode.BadRequest, TwoFactorErrors.InvalidCode);
        using (var next = await PostAsync(third, "/verify", new { method = "authenticator", code = CodeAt(secret, Math.Max(CurrentStep(), setupStep + 1)) }))
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.PlatformRecoveryCodes.CountAsync(code => code.UserId == userId && code.UsedAtUtc != null, Ct));
        // Una fila por acción (E9): el ingreso con un código de recuperación queda sólo como ese ingreso.
        Assert.False(await db.SecurityEvents.AnyAsync(item => item.Type == SecurityEventType.PlatformRecoveryCodeUsed
            && item.TargetAccountId == userId, Ct));
        Assert.True(await db.LoginAudits.AnyAsync(audit => audit.UserId == userId && audit.Succeeded
            && audit.Method == LoginAuditMethod.RecoveryCode, Ct));
    }

    [Fact]
    public async Task Ten_wrong_codes_lock_the_account_with_retry_after_and_stop_accepting_codes()
    {
        var (client, userId, email) = await OperatorAsync();
        using (var first = await FirstFactorAsync(client, email)) Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var secret = (await ChallengeAsync(client)).GetProperty("secret").GetString()!;
        var wrong = Wrong(PlatformFactorJourney.Code(secret, factory.Services));
        for (var attempt = 1; attempt < 10; attempt++)
            await AssertErrorAsync(await PostAsync(client, "/verify", new { method = "authenticator", code = wrong }),
                HttpStatusCode.BadRequest, TwoFactorErrors.InvalidCode);

        using (var locked = await PostAsync(client, "/verify", new { method = "authenticator", code = wrong }))
            AssertLocked(locked, await JsonAsync(locked));
        // Bloqueada, ni siquiera el código correcto sirve.
        using (var stillLocked = await PostAsync(client, "/verify", new { method = "authenticator", code = PlatformFactorJourney.Code(secret, factory.Services) }))
            AssertLocked(stillLocked, await JsonAsync(stillLocked));
        Assert.False(await HasAuthenticatorAsync(userId));
    }

    [Fact]
    public async Task Retrying_the_same_key_after_a_lost_response_replays_the_same_session_once()
    {
        var (client, userId, email) = await OperatorAsync();
        await SetUpAsync(client, userId, email);

        // Un navegador sin manejo automático de cookies, para repetir el pedido como si no hubiera llegado la respuesta.
        using var browser = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        string challengeCookie;
        using (var first = await FirstFactorAsync(browser, email))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            challengeCookie = Assert.Single(SetCookies(first), cookie => cookie.StartsWith(ChallengeCookie + "=", StringComparison.Ordinal)).Split(';')[0];
        }
        var key = Guid.NewGuid();
        var code = await PlatformFactorJourney.CurrentCodeAsync(factory.Services, userId, Ct);
        var body = new { method = "authenticator", code };
        string firstCookie;
        using (var verified = await SendWithCookieAsync(browser, "/verify", body, challengeCookie, key))
        {
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            Assert.False(verified.Headers.Contains("Idempotent-Replayed"));
            firstCookie = Assert.Single(SetCookies(verified), cookie => cookie.StartsWith(PlatformCookie + "=", StringComparison.Ordinal));
        }
        using (var replayed = await SendWithCookieAsync(browser, "/verify", body, challengeCookie, key))
        {
            Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
            Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
            Assert.True(replayed.Headers.CacheControl?.NoStore);
            Assert.Equal("authorize", (await JsonAsync(replayed)).GetProperty("next").GetString());
            Assert.Contains(SetCookies(replayed), cookie => cookie.StartsWith(PlatformCookie + "=", StringComparison.Ordinal));
        }
        // Otra clave no reutiliza el intervalo ni el desafío ya completado.
        using (var other = await SendWithCookieAsync(browser, "/verify", body, challengeCookie, Guid.NewGuid()))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
            Assert.Equal(TwoFactorErrors.ChallengeExpiredCode, (await JsonAsync(other)).GetProperty("code").GetString());
        }
        // La misma clave con otro cuerpo es otra operación.
        using (var reused = await SendWithCookieAsync(browser, "/verify", new { method = "authenticator", code = Wrong(code) }, challengeCookie, key))
            Assert.Equal((HttpStatusCode)422, reused.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.PlatformSessions.CountAsync(session => session.UserId == userId, Ct));
        Assert.Single(await db.PlatformFactorOperations.Where(operation => operation.IdempotencyKey == key).ToArrayAsync(Ct));
        Assert.NotEmpty(firstCookie);
    }

    [Fact]
    public async Task An_authorize_without_a_current_second_factor_session_issues_no_tokens()
    {
        var (client, userId, email) = await OperatorAsync();
        await SetUpAsync(client, userId, email);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);

        // La sesión del navegador dejó de ser evidencia (por ejemplo, una sesión anterior a la E9): vuelve al ingreso.
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlatformSessions
                .Where(session => session.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, Now()), Ct);
        using var authorize = await client.GetAsync(AuthorizePath(), Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var location = authorize.Headers.Location!.OriginalString;
        Assert.StartsWith("/login?returnUrl=", location, StringComparison.Ordinal);
        Assert.False(QueryHelpers.ParseQuery(location[location.IndexOf('?', StringComparison.Ordinal)..]).ContainsKey("code"));
    }

    [Fact]
    public async Task Public_doors_and_login_links_never_reach_the_platform_factor()
    {
        var (client, _, email) = await OperatorAsync();
        foreach (var returnUrl in new[] { "/connect/authorize?access=consumer", "/invitacion" })
        {
            using var elsewhere = await FirstFactorAsync(client, email, returnUrl);
            Assert.Equal(HttpStatusCode.Forbidden, elsewhere.StatusCode);
            Assert.Equal(AccessErrors.PlatformOnlyCode, (await JsonAsync(elsewhere)).GetProperty("code").GetString());
            Assert.DoesNotContain(SetCookies(elsewhere), cookie => cookie.StartsWith(ChallengeCookie + "=", StringComparison.Ordinal));
        }
        // Sin desafío de este navegador, las rutas del factor responden como un desafío vencido.
        using var challenge = await client.GetAsync(PlatformFactorJourney.Path, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
    }

    private async Task SetUpAsync(HttpClient client, Guid userId, string email)
    {
        using (var verified = await FirstFactorAsync(client, email)) Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await PlatformFactorJourney.CompleteAsync(factory.Services, client, userId, Ct);
    }

    private async Task<(HttpClient Client, Guid UserId, string Email)> OperatorAsync()
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId,
            [PlatformPermissions.Accounts.Read, PlatformPermissions.Operators.Manage], Ct);
        return (client, account.UserId, account.Email.Value);
    }

    private async Task<HttpResponseMessage> FirstFactorAsync(HttpClient client, string email, string returnUrl = PlatformReturn)
    {
        // El código del registro es de la preparación: queda fuera de la ventana del nuevo ingreso.
        await using (var connection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                UPDATE identity."LoginCodes" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour' WHERE "Destination" = @email
                """, connection);
            command.Parameters.AddWithValue("email", email);
            await command.ExecuteNonQueryAsync(Ct);
        }
        using (var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email }, Ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        return await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl }, Ct);
    }

    private Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body) =>
        PlatformFactorJourney.PostAsync(factory.Services, client, PlatformFactorJourney.Path + path, body, Ct);

    private async Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, string path, object body, string cookie, Guid key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PlatformFactorJourney.Path + path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        request.Headers.Add("Origin", factory.Services.GetRequiredService<IPublicOrigin>().Value!.GetLeftPart(UriPartial.Authority));
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> ChallengeAsync(HttpClient client)
    {
        using var response = await client.GetAsync(PlatformFactorJourney.Path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    private async Task<int> FailuresAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking()
            .Where(user => user.Id == userId).Select(user => user.AccessFailedCount).SingleAsync(Ct);
    }

    private async Task<bool> HasAuthenticatorAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlatformAuthenticators
            .AnyAsync(authenticator => authenticator.UserId == userId, Ct);
    }

    private static void AssertLocked(HttpResponseMessage response, JsonElement problem)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(AccountErrors.LockedOutCode, problem.GetProperty("code").GetString());
        var retryAfter = problem.GetProperty("retryAfter").GetInt32();
        Assert.InRange(retryAfter, 1, 900);
        Assert.Equal(retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Assert.Single(response.Headers.GetValues("Retry-After")));
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal(code, (await JsonAsync(response)).GetProperty("code").GetString());
        }
    }

    private static string Wrong(string code) => code[..^1] + (code[^1] == '9' ? '0' : (char)(code[^1] + 1));

    private static string[] SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? [.. values] : [];

    private static string AuthorizePath() => "/connect/authorize?client_id=web&response_type=code"
        + "&redirect_uri=" + Uri.EscapeDataString("https://localhost:5174/auth/callback") + "&scope=openid%20profile%20email%20api"
        + "&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&access=platform";

    private DateTime Now() => factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

    private long CurrentStep() => factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds() / 30;

    private static string CodeAt(string secret, long step) =>
        ArquitecturaBaseMultitenant.Infrastructure.Security.PlatformFactorSecrets.Totp(PlatformFactorJourney.Base32(secret), step);

    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }
}
