using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>ADR 0044: las cookies pública y de Plataforma conviven y se cierran por su propio alcance.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformSessionTests(ApiFactory factory) : IDisposable
{
    private readonly WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder =>
        builder.UseSetting("Authentication:LoginCode:ResendCooldownSeconds", "0"));
    public void Dispose() => host.Dispose();
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer", "/login")]
    [InlineData("business", "/login/empresa")]
    public async Task A_platform_cookie_does_not_participate_in_public_authorize(string access, string door)
    {
        var account = await OperatorAsync();
        using var browser = Client();
        using var signedIn = await LoginAsync(browser, account.Email.Value, "platform");
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Contains(signedIn.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Identity.Platform=", StringComparison.Ordinal));
        Assert.DoesNotContain(signedIn.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));

        using var response = await browser.GetAsync(AuthorizePath(access), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(door + "?returnUrl=", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    public async Task Both_cookies_authorize_their_own_account_and_public_cookie_cannot_enter_platform(string publicAccess)
    {
        var operatorAccount = await OperatorAsync();
        using var browser = Client();
        var person = await AccountJourney.RegisterAsync(host, browser, Ct);
        if (publicAccess == "business")
            await OrganizationJourney.StageAsync(factory, person, "Convivencia " + Guid.NewGuid().ToString("N"), Ct);
        using (var noPlatform = await browser.GetAsync(AuthorizePath("platform"), Ct))
            Assert.StartsWith("/login?returnUrl=", noPlatform.Headers.Location!.OriginalString, StringComparison.Ordinal);
        using (var signedIn = await LoginAsync(browser, operatorAccount.Email.Value, "platform"))
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

        var platform = await AuthorizeAsync(browser, "platform");
        var consumer = await AuthorizeAsync(browser, publicAccess);

        Assert.Equal(operatorAccount.UserId, platform.UserId);
        Assert.Equal(person.UserId, consumer.UserId);
        using var platformAgain = await browser.GetAsync(AuthorizePath("platform"), Ct);
        Assert.True(QueryHelpers.ParseQuery(platformAgain.Headers.Location!.Query).ContainsKey("code"));
    }

    [Theory]
    [InlineData("platform", "consumer", ".AspNetCore.Identity.Platform=")]
    [InlineData("consumer", "platform", ".AspNetCore.Identity.Application=")]
    public async Task Logout_revokes_only_the_current_cookie_and_tokens(string exiting, string remaining, string clearedCookie)
    {
        var operatorAccount = await OperatorAsync();
        using var browser = Client();
        var person = await AccountJourney.RegisterAsync(host, browser, Ct);
        using (var signedIn = await LoginAsync(browser, operatorAccount.Email.Value, "platform"))
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var publicTokens = await AuthorizeAsync(browser, "consumer");
        var platformTokens = await AuthorizeAsync(browser, "platform");
        var tokens = exiting == "platform" ? platformTokens : publicTokens;
        using var logout = await browser.GetAsync("/connect/logout?client_id=web&post_logout_redirect_uri=https%3A%2F%2Flocalhost%3A5174%2F&id_token_hint=" + Uri.EscapeDataString(tokens.IdToken), Ct);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), value => value.StartsWith(clearedCookie + ";", StringComparison.Ordinal));

        using var noSession = await browser.GetAsync(AuthorizePath(exiting), Ct);
        Assert.StartsWith("/login?returnUrl=", noSession.Headers.Location!.OriginalString, StringComparison.Ordinal);
        var other = await AuthorizeAsync(browser, remaining);
        Assert.Equal(remaining == "platform" ? operatorAccount.UserId : person.UserId, other.UserId);
        using var revoked = await RefreshAsync(browser, tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, revoked.StatusCode);
        using var preserved = await RefreshAsync(browser, remaining == "platform" ? platformTokens.RefreshToken : publicTokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    public async Task An_accepted_operator_never_receives_a_public_cookie(string access)
    {
        var account = await OperatorAsync();
        using var browser = Client();
        var person = await AccountJourney.RegisterAsync(host, browser, Ct);
        using (var signedIn = await LoginAsync(browser, account.Email.Value, "platform"))
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

        using var verified = await LoginAsync(browser, account.Email.Value, access);

        Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
        using var problem = await verified.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(AccessErrors.PlatformOnlyCode, problem!.RootElement.GetProperty("code").GetString());
        Assert.False(verified.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(value => value.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal)));
        Assert.Equal(person.UserId, (await AuthorizeAsync(browser, "consumer")).UserId);
        Assert.Equal(account.UserId, (await AuthorizeAsync(browser, "platform")).UserId);
    }

    [Fact]
    public async Task Revoking_a_platform_accounts_stamp_keeps_the_other_accounts_public_cookie()
    {
        var account = await OperatorAsync();
        using var browser = Client();
        var person = await AccountJourney.RegisterAsync(host, browser, Ct);
        using (var signedIn = await LoginAsync(browser, account.Email.Value, "platform"))
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var platform = await AuthorizeAsync(browser, "platform");
        await RevokeAsync(account.UserId);

        using var rejected = await browser.GetAsync(AuthorizePath("platform"), Ct);
        Assert.StartsWith("/login?returnUrl=", rejected.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal(person.UserId, (await AuthorizeAsync(browser, "consumer")).UserId);
        using var refresh = await RefreshAsync(browser, platform.RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
    }

    [Theory]
    [InlineData("revoke-sessions")]
    [InlineData("deletion")]
    public async Task Closing_sessions_or_deleting_an_account_invalidates_both_cookies_and_tokens(string action)
    {
        var managerAccount = await OperatorAsync();
        using var manager = Client();
        using (var signedIn = await LoginAsync(manager, managerAccount.Email.Value, "platform"))
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        await AccountJourney.AuthorizeAsync(manager, "platform", Ct);
        using var browser = Client();
        var account = await AccountJourney.RegisterAsync(host, browser, Ct);
        var consumerTokens = await AuthorizeAsync(browser, "consumer");
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        using (var signedIn = await LoginAsync(browser, account.Email.Value, "platform"))
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var platformTokens = await AuthorizeAsync(browser, "platform");
        using var closed = await PlatformJourney.PostAsync(manager, $"/api/platform/accounts/{account.UserId:D}/{action}",
            new { reason = "Cierre solicitado por la cuenta." }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);

        foreach (var access in new[] { "consumer", "platform" })
        {
            using var rejected = await browser.GetAsync(AuthorizePath(access), Ct);
            Assert.StartsWith("/login?returnUrl=", rejected.Headers.Location!.OriginalString, StringComparison.Ordinal);
        }
        foreach (var tokens in new[] { consumerTokens, platformTokens })
        {
            using var rejected = await RefreshAsync(browser, tokens.RefreshToken);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
    }

    private HttpClient Client() => host.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
    });

    private async Task<AccountJourney> OperatorAsync()
    {
        using var setup = Client();
        var account = await AccountJourney.RegisterAsync(host, setup, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        return account;
    }

    private async Task<HttpResponseMessage> LoginAsync(HttpClient browser, string email, string access)
    {
        using var requested = await AccountJourney.PostAsync(browser, "/api/auth/login-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(host.Services, email, Ct);
        return await AccountJourney.PostAsync(browser, "/api/auth/login-code/verify", new { email, code, returnUrl = AuthorizePath(access) }, Ct);
    }

    private async Task RevokeAsync(Guid userId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await services.GetRequiredService<ISignInService>().RevokeSessionsAsync(userId, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<Tokens> AuthorizeAsync(HttpClient browser, string access)
    {
        using var authorization = await browser.GetAsync(AuthorizePath(access), Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorization.StatusCode);
        var code = QueryHelpers.ParseQuery(authorization.Headers.Location!.Query)["code"].ToString();
        Assert.False(string.IsNullOrEmpty(code));
        using var exchanged = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["client_id"] = "web",
            ["redirect_uri"] = "https://localhost:5174/auth/callback", ["code_verifier"] = Verifier,
        }), Ct);
        Assert.Equal(HttpStatusCode.OK, exchanged.StatusCode);
        using var tokens = await exchanged.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var root = tokens!.RootElement;
        var idToken = root.GetProperty("id_token").GetString()!;
        using var claims = JsonDocument.Parse(WebEncoders.Base64UrlDecode(idToken.Split('.')[1]));
        return new Tokens(claims.RootElement.GetProperty("sub").GetGuid(), idToken, root.GetProperty("refresh_token").GetString()!);
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient browser, string refreshToken) => browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "refresh_token", ["client_id"] = "web", ["refresh_token"] = refreshToken,
    }), Ct);

    private static string AuthorizePath(string access) => "/connect/authorize?client_id=web&response_type=code"
        + "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback&scope=openid%20profile%20email%20api%20offline_access"
        + "&code_challenge=" + WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier))) + "&code_challenge_method=S256&access=" + access;

    private sealed record Tokens(Guid UserId, string IdToken, string RefreshToken);
}
