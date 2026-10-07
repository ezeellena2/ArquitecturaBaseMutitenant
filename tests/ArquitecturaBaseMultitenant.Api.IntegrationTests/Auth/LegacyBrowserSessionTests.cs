using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>Las cookies firmadas anteriores a ADR 0044 se recuperan sin cerrar la sesión de Plataforma.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class LegacyBrowserSessionTests(ApiFactory factory) : IDisposable
{
    private readonly WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder =>
        builder.UseSetting("Authentication:LoginCode:ResendCooldownSeconds", "0"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => host.Dispose();

    [Theory]
    [InlineData("consumer", "/login")]
    [InlineData("business", "/login/empresa")]
    public async Task An_old_public_operator_cookie_expires_and_opens_the_selected_login_without_closing_platform(
        string access, string door)
    {
        using var setup = Client();
        var person = await AccountJourney.RegisterAsync(host, setup, Ct);
        if (access == "business")
            await OrganizationJourney.StageAsync(factory, person, "Legado " + Guid.NewGuid().ToString("N"), Ct);
        var operatorAccount = await AccountJourney.RegisterAsync(host, setup, Ct);
        await PlatformOperators.GrantAsync(host.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        using var browser = Client();
        using (var signedIn = await LoginAsync(browser, operatorAccount.Email.Value, "platform"))
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var legacyCookie = await OldPublicCookieAsync(operatorAccount.UserId);
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthorizePath(access));
        request.Headers.Add("Cookie", legacyCookie);

        using var recovered = await browser.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Redirect, recovered.StatusCode);
        var location = recovered.Headers.Location!.OriginalString;
        Assert.StartsWith(door + "?returnUrl=", location, StringComparison.Ordinal);
        var returnedQuery = new Uri(browser.BaseAddress!, recovered.Headers.Location).Query;
        Assert.Equal(AuthorizePath(access), QueryHelpers.ParseQuery(returnedQuery)["returnUrl"].ToString());
        var expired = Assert.Single(recovered.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 01 Jan 1970", expired, StringComparison.Ordinal);
        Assert.DoesNotContain(recovered.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(".AspNetCore.Identity.Platform=;", StringComparison.Ordinal));
        using (var platform = await browser.GetAsync(AuthorizePath("platform"), Ct))
            Assert.True(QueryHelpers.ParseQuery(platform.Headers.Location!.Query).ContainsKey("code"));
        using (var emptyPublicSession = await browser.GetAsync(AuthorizePath(access), Ct))
            Assert.StartsWith(door + "?returnUrl=", emptyPublicSession.Headers.Location!.OriginalString, StringComparison.Ordinal);

        using (var signedIn = await LoginAsync(browser, person.Email.Value, access))
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        await AccountJourney.AuthorizeAsync(browser, access, Ct);
        using var me = await browser.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var profile = await me.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(Ct);
        Assert.Equal(person.UserId, profile!.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_person_without_business_membership_keeps_the_designed_not_member_result_and_public_session()
    {
        using var browser = Client();
        await AccountJourney.RegisterAsync(host, browser, Ct);

        using var rejected = await browser.GetAsync(AuthorizePath("business"), Ct);

        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        var parameters = QueryHelpers.ParseQuery(rejected.Headers.Location!.Query);
        Assert.Equal("access_denied", parameters["error"].ToString());
        Assert.Equal(AccessErrors.NotMemberCode, parameters["error_description"].ToString());
        Assert.False(parameters.ContainsKey("code"));
        var cookies = rejected.Headers.TryGetValues("Set-Cookie", out var setCookies) ? setCookies : [];
        Assert.DoesNotContain(cookies, value =>
            value.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
        using var personal = await browser.GetAsync(AuthorizePath("consumer"), Ct);
        Assert.True(QueryHelpers.ParseQuery(personal.Headers.Location!.Query).ContainsKey("code"));
    }

    private HttpClient Client() => host.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
    });

    private async Task<string> OldPublicCookieAsync(Guid userId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var manager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByIdAsync(userId.ToString("D"));
        Assert.NotNull(user);
        var principal = await provider.GetRequiredService<SignInManager<ApplicationUser>>().CreateUserPrincipalAsync(user);
        var startedAtUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(-2);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true, IssuedUtc = startedAtUtc, ExpiresUtc = startedAtUtc.AddDays(30),
        };
        properties.Items[BrowserSessionKeys.CookieSessionId] = Guid.NewGuid().ToString("N");
        properties.Items[BrowserSessionKeys.StartedAtUtc] = BrowserSessionKeys.FormatStartedAtUtc(startedAtUtc.UtcDateTime);
        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        return cookie.Cookie.Name + "=" + cookie.TicketDataFormat.Protect(
            new AuthenticationTicket(principal, properties, IdentityConstants.ApplicationScheme));
    }

    private async Task<HttpResponseMessage> LoginAsync(HttpClient browser, string email, string access)
    {
        using var requested = await AccountJourney.PostAsync(browser, "/api/auth/login-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(host.Services, email, Ct);
        return await AccountJourney.PostAsync(browser, "/api/auth/login-code/verify",
            new { email, code, returnUrl = AuthorizePath(access) }, Ct);
    }

    private static string AuthorizePath(string access) =>
        "/connect/authorize?client_id=web&response_type=code"
        + "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback&scope=openid%20profile%20email%20api"
        + "&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&code_challenge_method=S256&access=" + access;
}
