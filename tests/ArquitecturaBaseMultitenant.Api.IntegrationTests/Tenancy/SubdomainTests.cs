using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity.OpenIddict;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Comprueba el host público, su estado real y la independencia del tenant del token.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class SubdomainTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

    [Theory]
    [InlineData("empresa-a.example.test:5174", true)]
    [InlineData("EMPRESA-A.EXAMPLE.TEST:5174", true)]
    [InlineData("a.example.test:5174", true)]
    [InlineData("example.test:5174", false)]
    [InlineData("a.b.example.test:5174", false)]
    [InlineData("empresa-a.other.test:5174", false)]
    [InlineData("empresa-a.example.test.evil.test:5174", false)]
    [InlineData("127.0.0.1:5174", false)]
    [InlineData("[::1]:5174", false)]
    [InlineData("empresa-a.example.test:5175", false)]
    [InlineData("empresa-a.example.test", false)]
    [InlineData("ab.example.test:5174", false)]
    [InlineData("-empresa.example.test:5174", false)]
    [InlineData("empresa_.example.test:5174", false)]
    [InlineData("émpresa.example.test:5174", false)]
    [InlineData(" empresa-a.example.test:5174", false)]
    public async Task Only_a_single_label_over_the_main_domain_is_an_organization_host(string host, bool expected)
    {
        var request = PublicRequest(host);
        var reader = new RecordingReader();
        var publicSite = new PublicSiteContext();
        var middleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://example.test:5174/")));

        await middleware.InvokeAsync(request, publicSite, reader);

        Assert.Equal(expected ? 1 : 0, reader.Calls);
        Assert.Equal(expected, publicSite.BusinessTenantId is not null);
        if (expected) Assert.Equal(host.Split('.')[0].ToLowerInvariant(), publicSite.Slug);
    }

    [Fact]
    public async Task The_default_https_port_matches_the_issuer_and_an_ip_issuer_never_resolves()
    {
        var middleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://example.test/")));
        var reader = new RecordingReader();
        var publicSite = new PublicSiteContext();
        await middleware.InvokeAsync(PublicRequest("empresa.example.test"), publicSite, reader);
        Assert.NotNull(publicSite.BusinessTenantId);
        Assert.Equal(1, reader.Calls);

        var ipMiddleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://127.0.0.1:5174/")));
        await ipMiddleware.InvokeAsync(PublicRequest("empresa.127.0.0.1:5174"), new PublicSiteContext(), reader);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task Private_routes_do_not_consult_the_public_site_reader()
    {
        var request = PublicRequest("empresa-a.example.test:5174");
        request.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "private"));
        var reader = new RecordingReader();
        var publicSite = new PublicSiteContext();
        var middleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://example.test:5174/")));

        await middleware.InvokeAsync(request, publicSite, reader);

        Assert.Equal(0, reader.Calls);
        Assert.Null(publicSite.BusinessTenantId);
        Assert.Null(publicSite.Slug);
    }

    [Fact]
    public async Task The_public_site_filter_uses_the_resolved_context_without_a_second_query()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var request = PublicRequest("empresa-a.example.test:5174");
        request.RequestServices = provider;
        var reader = new RecordingReader();
        var publicSite = new PublicSiteContext();
        var middleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://example.test:5174/")));
        await middleware.InvokeAsync(request, publicSite, reader);
        var filter = new PublicSiteFilter(publicSite, provider.GetRequiredService<ProblemDetailsFactory>());
        var context = new AuthorizationFilterContext(
            new ActionContext(request, new RouteData(), new ActionDescriptor()), []);

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task A_published_organization_subdomain_resolves_its_site()
    {
        var tenant = await CreateSiteAsync();
        using var client = Client();
        using var response = await client.GetAsync(SiteUrl(tenant.Slug!), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(tenant.Id, body!.RootElement.GetProperty("businessTenantId").GetGuid());
        Assert.Equal(tenant.Slug, body.RootElement.GetProperty("slug").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("privateTenantId").ValueKind);
    }

    [Fact]
    public async Task Main_domain_reserved_and_unknown_subdomains_resolve_no_site()
    {
        using var client = Client();
        await factory.Services.SeedDatabaseAsync(Ct);
        await AssertNotFoundAsync(client, "https://localhost:5174/test/access/public-site");
        await AssertNotFoundAsync(client, SiteUrl("unknown-" + Guid.NewGuid().ToString("N")));
        foreach (var reserved in ReservedTenantSlug.Defaults)
            await AssertNotFoundAsync(client, SiteUrl(reserved.Slug));

        // La reserva también bloquea un registro histórico con página publicada.
        await CreateSiteAsync(slug: "www");
        await AssertNotFoundAsync(client, SiteUrl("www"));
    }

    [Theory]
    [InlineData(TenantStatus.Active, false, false, true)]
    [InlineData(TenantStatus.Active, true, true, true)]
    [InlineData(TenantStatus.Active, true, false, false)]
    [InlineData(TenantStatus.Suspended, true, false, true)]
    [InlineData(TenantStatus.Closed, true, false, true)]
    [InlineData(TenantStatus.PendingApproval, true, false, true)]
    [InlineData(TenantStatus.Provisioning, true, false, true)]
    public async Task Unpublished_blocked_missing_or_inactive_sites_are_indistinguishable(
        TenantStatus status, bool published, bool blocked, bool withPage)
    {
        var tenant = await CreateSiteAsync(status, published, blocked, withPage);
        using var client = Client();

        await AssertNotFoundAsync(client, SiteUrl(tenant.Slug!));
    }

    [Fact]
    public async Task The_host_never_sets_the_tenant_of_a_session()
    {
        using var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var siteA = await CreateSiteAsync();
        var siteB = await CreateSiteAsync(ownerId: account.UserId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, siteB.Id);

        using var publicResponse = await client.GetAsync(SiteUrl(siteA.Slug!), Ct);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        using var publicBody = await publicResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(siteA.Id, publicBody!.RootElement.GetProperty("businessTenantId").GetGuid());
        Assert.Equal(JsonValueKind.Null, publicBody.RootElement.GetProperty("privateTenantId").ValueKind);

        using var privateResponse = await client.GetAsync(
            $"https://{siteA.Slug}.localhost:5174/test/access/private-context", Ct);
        Assert.Equal(HttpStatusCode.OK, privateResponse.StatusCode);
        using var privateBody = await privateResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(siteB.Id, privateBody!.RootElement.GetProperty("tenantId").GetGuid());
        Assert.Equal(JsonValueKind.Null, privateBody.RootElement.GetProperty("businessTenantId").ValueKind);
    }

    [Theory]
    [InlineData("/auth/callback")]
    [InlineData("/silent-renew.html")]
    public async Task Published_slug_callbacks_continue_to_the_main_domain_login(string path)
    {
        var site = await CreateSiteAsync();
        using var client = Client();
        var redirect = PublicOrigin(site.Slug!) + path;
        using var response = await client.GetAsync(AuthorizePath(redirect), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login?returnUrl=", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        var resume = QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), response.Headers.Location).Query)
            ["returnUrl"].ToString();
        Assert.Equal(redirect, QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), resume).Query)
            ["redirect_uri"].ToString());
    }

    [Theory]
    [InlineData(TenantStatus.Active, false, false, true)]
    [InlineData(TenantStatus.Active, true, true, true)]
    [InlineData(TenantStatus.Active, true, false, false)]
    [InlineData(TenantStatus.Suspended, true, false, true)]
    [InlineData(TenantStatus.Closed, true, false, true)]
    [InlineData(TenantStatus.PendingApproval, true, false, true)]
    public async Task Redirect_uri_only_for_published_and_active_slugs(
        TenantStatus status, bool published, bool blocked, bool withPage)
    {
        var site = await CreateSiteAsync(status, published, blocked, withPage);
        using var client = Client();
        using var response = await client.GetAsync(AuthorizePath(PublicOrigin(site.Slug!) + "/auth/callback"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Unknown_and_reserved_slugs_never_receive_an_authorization_redirect()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = Client();
        foreach (var slug in ReservedTenantSlug.Defaults.Select(item => item.Slug)
            .Append("unknown-" + Guid.NewGuid().ToString("N")))
        {
            using var response = await client.GetAsync(AuthorizePath(PublicOrigin(slug) + "/auth/callback"), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null(response.Headers.Location);
        }
    }

    [Theory]
    [InlineData("http://{0}.localhost:5174/auth/callback")]
    [InlineData("https://{0}.localhost:5175/auth/callback")]
    [InlineData("https://{0}.localhost/auth/callback")]
    [InlineData("https://{0}.other.test:5174/auth/callback")]
    [InlineData("https://{0}.localhost.evil.test:5174/auth/callback")]
    [InlineData("https://other.{0}.localhost:5174/auth/callback")]
    [InlineData("https://{0}.localhost:5174/other")]
    [InlineData("https://{0}.localhost:5174/Auth/Callback")]
    [InlineData("https://{0}.localhost:5174/auth/%63allback")]
    [InlineData("https://{0}.localhost:5174/other/../auth/callback")]
    [InlineData("https://{0}.localhost:5174/auth/callback?returnTo=evil")]
    [InlineData("https://{0}.localhost:5174/auth/callback#fragment")]
    [InlineData("https://user@{0}.localhost:5174/auth/callback")]
    public async Task Redirect_uri_rejects_other_origins_paths_and_uri_components(string template)
    {
        var site = await CreateSiteAsync();
        using var client = Client();
        var redirect = string.Format(CultureInfo.InvariantCulture, template, site.Slug);
        using var response = await client.GetAsync(AuthorizePath(redirect), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Dynamic_returns_apply_only_to_web_and_preserve_fixed_allowlists_without_storing_wildcards()
    {
        var site = await CreateSiteAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var web = await applications.FindByClientIdAsync("web", Ct);
        Assert.NotNull(web);
        var callback = PublicOrigin(site.Slug!) + "/auth/callback";
        var home = PublicOrigin(site.Slug!) + "/";

        Assert.True(await applications.ValidateRedirectUriAsync(web, "https://localhost:5174/auth/callback", Ct));
        Assert.True(await applications.ValidatePostLogoutRedirectUriAsync(web, "https://localhost:5174/invitacion", Ct));
        Assert.True(await applications.ValidateRedirectUriAsync(web, callback, Ct));
        Assert.True(await applications.ValidatePostLogoutRedirectUriAsync(web, home, Ct));
        Assert.DoesNotContain(callback, await applications.GetRedirectUrisAsync(web, Ct));
        Assert.DoesNotContain(home, await applications.GetPostLogoutRedirectUrisAsync(web, Ct));
        Assert.DoesNotContain(await applications.GetRedirectUrisAsync(web, Ct), value => value.Contains('*'));
        Assert.DoesNotContain(await applications.GetPostLogoutRedirectUrisAsync(web, Ct), value => value.Contains('*'));

        object? other = null;
        var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "other-" + Guid.NewGuid().ToString("N"), ClientType = ClientTypes.Public,
            };
            descriptor.RedirectUris.Add(new Uri("https://localhost:5174/auth/callback"));
            descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:5174/"));
            other = await applications.CreateAsync(descriptor, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(created.IsSuccess);
        Assert.NotNull(other);
        Assert.True(await applications.ValidateRedirectUriAsync(other, "https://localhost:5174/auth/callback", Ct));
        Assert.False(await applications.ValidateRedirectUriAsync(other, callback, Ct));
        Assert.False(await applications.ValidatePostLogoutRedirectUriAsync(other, home, Ct));

        var matches = new List<object>();
        await foreach (var application in applications.FindByPostLogoutRedirectUriAsync(home, Ct)) matches.Add(application);
        Assert.Equal("web", await applications.GetClientIdAsync(Assert.Single(matches), Ct));
    }

    [Fact]
    public async Task Dynamic_returns_use_the_default_https_port_and_never_derive_subdomains_from_an_ip_issuer()
    {
        var reader = new RecordingReader();
        var validator = new SubdomainRedirectUriValidator(new StubOrigin(new Uri("https://example.test/")), reader);
        Assert.True(await validator.ValidateRedirectUriAsync("https://empresa.example.test/auth/callback", Ct));
        Assert.True(await validator.ValidatePostLogoutRedirectUriAsync("https://empresa.example.test:443/", Ct));
        Assert.False(await validator.ValidateRedirectUriAsync("https://empresa.example.test:444/auth/callback", Ct));
        Assert.False(await validator.ValidateRedirectUriAsync("https://ab.example.test/auth/callback", Ct));
        Assert.False(await validator.ValidateRedirectUriAsync("https://empresa.example.test/auth/%63allback", Ct));
        Assert.False(await validator.ValidateRedirectUriAsync("https://empresa.example.test/other/../auth/callback", Ct));
        Assert.False(await validator.ValidateRedirectUriAsync("https://127.0.0.1/auth/callback", Ct));
        Assert.Equal(2, reader.Calls);

        var ipValidator = new SubdomainRedirectUriValidator(new StubOrigin(new Uri("https://127.0.0.1:5174/")), reader);
        Assert.False(await ipValidator.ValidateRedirectUriAsync("https://empresa.127.0.0.1:5174/auth/callback", Ct));
        Assert.Equal(2, reader.Calls);
    }

    [Fact]
    public async Task Withdrawing_publication_immediately_rejects_authorization_and_logout_returns()
    {
        var site = await CreateSiteAsync();
        var callback = PublicOrigin(site.Slug!) + "/auth/callback";
        using var browser = Client();
        using (var accepted = await browser.GetAsync(AuthorizePath(callback), Ct))
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(site.Id, TenantKind.Business);
            var changed = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var page = await services.GetRequiredService<ApplicationDbContext>().PublicPages
                    .SingleAsync(item => item.BusinessTenantId == site.Id, ct);
                return page.Unpublish();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(changed.IsSuccess);
        }

        using var authorize = await browser.GetAsync(AuthorizePath(callback), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, authorize.StatusCode);
        Assert.Null(authorize.Headers.Location);
        using var logout = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/logout",
            "post_logout_redirect_uri", PublicOrigin(site.Slug!) + "/"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        Assert.Null(logout.Headers.Location);
    }

    [Fact]
    public async Task Code_issued_for_a_subdomain_is_exchanged_on_its_own_origin_with_the_fixed_issuer()
    {
        using var browser = Client();
        var account = await AccountJourney.RegisterAsync(factory, browser, Ct);
        var site = await CreateSiteAsync();
        var tokens = await AuthorizeAndExchangeAsync(browser, PublicOrigin(site.Slug!));
        using var claims = ReadClaims(tokens.IdToken);

        Assert.Equal("https://localhost:5174/", claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal("consumer", claims.RootElement.GetProperty("access").GetString());
        Assert.Equal("personal", claims.RootElement.GetProperty("tenant_kind").GetString());
        Assert.Equal(account.UserId, claims.RootElement.GetProperty("sub").GetGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        var personal = Assert.Single(await scope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>()
            .ListForUserAsync(account.UserId, Ct), item => item.Kind == TenantKind.Personal);
        Assert.Equal(personal.TenantId, claims.RootElement.GetProperty("tenant_id").GetGuid());
        Assert.NotEqual(site.Id, personal.TenantId);

        using var request = new HttpRequestMessage(HttpMethod.Get, PublicOrigin(site.Slug!) + "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await browser.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var me = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("consumer", me!.RootElement.GetProperty("access").GetString());
        Assert.Equal(personal.TenantId, me.RootElement.GetProperty("activeTenantId").GetGuid());
    }

    [Fact]
    public async Task A_published_subdomain_keeps_pkce_required_at_authorization_and_code_exchange()
    {
        var site = await CreateSiteAsync();
        var redirect = PublicOrigin(site.Slug!) + "/auth/callback";
        using var browser = Client();
        using (var missing = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/authorize",
            new Dictionary<string, string?> { ["client_id"] = "web", ["response_type"] = "code",
                ["redirect_uri"] = redirect, ["scope"] = "openid api", ["access"] = "consumer" }), Ct))
        {
            var result = missing.Headers.Location?.ToString() ?? await missing.Content.ReadAsStringAsync(Ct);
            Assert.Contains("invalid_request", result, StringComparison.Ordinal);
            Assert.DoesNotContain("/login", result, StringComparison.Ordinal);
        }

        await AccountJourney.RegisterAsync(factory, browser, Ct);
        using var authorized = await browser.GetAsync(AuthorizePath(redirect), Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);
        var code = QueryHelpers.ParseQuery(authorized.Headers.Location!.Query)["code"].ToString();
        Assert.False(string.IsNullOrEmpty(code));
        using var rejected = await browser.PostAsync(PublicOrigin(site.Slug!) + "/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["client_id"] = "web",
                ["code"] = code, ["redirect_uri"] = redirect, ["code_verifier"] = new string('B', 64) }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var error = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("invalid_grant", error!.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Userinfo_refresh_and_revocation_answer_on_the_subdomain_origin()
    {
        using var browser = Client();
        var account = await AccountJourney.RegisterAsync(factory, browser, Ct);
        var site = await CreateSiteAsync();
        var origin = PublicOrigin(site.Slug!);
        var tokens = await AuthorizeAndExchangeAsync(browser, origin);
        using var info = await UserInfoAsync(browser, origin, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        using var user = await info.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(account.UserId, user!.RootElement.GetProperty("sub").GetGuid());

        var refreshed = await ExchangeAsync(browser, origin, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = "web", ["refresh_token"] = tokens.RefreshToken,
        });
        using var claims = ReadClaims(refreshed.IdToken);
        Assert.Equal("https://localhost:5174/", claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal("consumer", claims.RootElement.GetProperty("access").GetString());
        using var revoked = await browser.PostAsync(origin + "/connect/revoke", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["client_id"] = "web", ["token"] = refreshed.AccessToken,
                ["token_type_hint"] = "access_token" }), Ct);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        using var denied = await UserInfoAsync(browser, origin, refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Logout_returns_to_a_published_subdomain_with_or_without_client_id_and_hint(bool clientId, bool hint)
    {
        using var browser = Client();
        await AccountJourney.RegisterAsync(factory, browser, Ct);
        var site = await CreateSiteAsync();
        var tokens = await AuthorizeAndExchangeAsync(browser, PublicOrigin(site.Slug!));
        var parameters = new Dictionary<string, string?> { ["post_logout_redirect_uri"] = PublicOrigin(site.Slug!) + "/" };
        if (clientId) parameters["client_id"] = "web";
        if (hint) parameters["id_token_hint"] = tokens.IdToken;
        using var response = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/logout", parameters), Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(PublicOrigin(site.Slug!) + "/", response.Headers.Location!.AbsoluteUri);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));

        using var revoked = await browser.PostAsync(PublicOrigin(site.Slug!) + "/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["client_id"] = "web",
                ["refresh_token"] = tokens.RefreshToken }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, revoked.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Logout_never_returns_to_an_unpublished_subdomain(bool clientId)
    {
        var site = await CreateSiteAsync(published: false);
        using var browser = Client();
        var parameters = new Dictionary<string, string?> { ["post_logout_redirect_uri"] = PublicOrigin(site.Slug!) + "/" };
        if (clientId) parameters["client_id"] = "web";
        using var response = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/logout", parameters), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task A_published_logout_return_preserves_the_fixed_return_behavior_for_an_invalid_optional_hint()
    {
        var site = await CreateSiteAsync();
        using var browser = Client();
        using var fixedReturn = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/logout",
            new Dictionary<string, string?> { ["client_id"] = "web", ["id_token_hint"] = "invalid-hint",
                ["post_logout_redirect_uri"] = "https://localhost:5174/" }), Ct);
        Assert.Equal(HttpStatusCode.Redirect, fixedReturn.StatusCode);
        Assert.Equal("https://localhost:5174/", fixedReturn.Headers.Location!.AbsoluteUri);

        using var response = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/logout",
            new Dictionary<string, string?> { ["client_id"] = "web", ["id_token_hint"] = "invalid-hint",
                ["post_logout_redirect_uri"] = PublicOrigin(site.Slug!) + "/" }), Ct);
        Assert.Equal(fixedReturn.StatusCode, response.StatusCode);
        Assert.Equal(PublicOrigin(site.Slug!) + "/", response.Headers.Location!.AbsoluteUri);

        using var unknown = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/logout",
            new Dictionary<string, string?> { ["client_id"] = "web", ["id_token_hint"] = "invalid-hint",
                ["post_logout_redirect_uri"] = "https://unknown.localhost:5174/" }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Null(unknown.Headers.Location);
    }

    [Theory]
    [InlineData("https://{0}.localhost:5174/auth/callback")]
    [InlineData("https://{0}.localhost:5174/?returnTo=evil")]
    [InlineData("https://{0}.localhost:5174/#fragment")]
    [InlineData("https://user@{0}.localhost:5174/")]
    [InlineData("https://other.{0}.localhost:5174/")]
    [InlineData("http://{0}.localhost:5174/")]
    [InlineData("https://{0}.localhost:5175/")]
    [InlineData("https://{0}.other.test:5174/")]
    public async Task Logout_rejects_other_origins_paths_and_uri_components(string template)
    {
        var site = await CreateSiteAsync();
        using var browser = Client();
        var redirect = string.Format(CultureInfo.InvariantCulture, template, site.Slug);
        using var response = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/logout",
            new Dictionary<string, string?> { ["post_logout_redirect_uri"] = redirect }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Silent_sign_in_never_creates_the_personal_space_or_changes_the_business_selection()
    {
        var account = await CreateBusinessOnlyAccountAsync();
        var business = await CreateSiteAsync(ownerId: account.UserId);
        using var browser = Client();
        await SignInBusinessAsync(browser, account, business.Id);
        await AccountJourney.AuthorizeAsync(browser, "business", Ct, business.Id);
        var redirect = PublicOrigin(business.Slug!) + "/silent-renew.html";
        using var response = await browser.GetAsync(AuthorizePath(redirect) + "&prompt=none", Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var parameters = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("login_required", parameters["error"].ToString());
        Assert.False(parameters.ContainsKey("code"));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            Assert.DoesNotContain(await services.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(account.UserId, Ct),
                item => item.Kind == TenantKind.Personal);
            Assert.Equal(business.Id, (await services.GetRequiredService<IUserRepository>().GetByIdAsync(account.UserId, Ct))!
                .LastBusinessTenantId);
        }

        // Elegir Personal de forma interactiva sí crea el espacio; después, la recuperación silenciosa funciona.
        await AuthorizeAndExchangeAsync(browser, PublicOrigin(business.Slug!));
        using var recovered = await browser.GetAsync(AuthorizePath(redirect) + "&prompt=none", Ct);
        Assert.Equal(HttpStatusCode.Redirect, recovered.StatusCode);
        Assert.True(QueryHelpers.ParseQuery(recovered.Headers.Location!.Query).ContainsKey("code"));
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Single(await check.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(account.UserId, Ct),
            item => item.Kind == TenantKind.Personal);
        Assert.Equal(business.Id, (await check.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(account.UserId, Ct))!
            .LastBusinessTenantId);
    }

    [Fact]
    public async Task Silent_business_switch_keeps_remembering_the_selected_organization()
    {
        using var browser = Client();
        var account = await AccountJourney.RegisterAsync(factory, browser, Ct);
        var first = await CreateSiteAsync(ownerId: account.UserId);
        var second = await CreateSiteAsync(ownerId: account.UserId);
        await AccountJourney.AuthorizeAsync(browser, "business", Ct, first.Id);
        var tokens = await AuthorizeAndExchangeAsync(browser, "https://localhost:5174", "business", second.Id, silent: true);
        using var claims = ReadClaims(tokens.IdToken);
        Assert.Equal("business", claims.RootElement.GetProperty("access").GetString());
        Assert.Equal(second.Id, claims.RootElement.GetProperty("tenant_id").GetGuid());
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Equal(second.Id, (await check.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(account.UserId, Ct))!
            .LastBusinessTenantId);
    }

    private async Task<AccountJourney> CreateBusinessOnlyAccountAsync()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var email = Email.Create("subdomain-business-" + Guid.NewGuid().ToString("N") + "@example.test").Value;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userId = Guid.Empty;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            userId = (await users.CreateAsync("Persona de empresa", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = LoginMethod.CreateEmail(userId, email);
            Assert.True(method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
            Assert.True(method.MakePrimary().IsSuccess);
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            await users.SetPrimaryEmailAsync(userId, email, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return new AccountJourney(userId, email);
    }

    private async Task SignInBusinessAsync(HttpClient browser, AccountJourney account, Guid tenantId)
    {
        using var requested = await AccountJourney.PostAsync(browser, "/api/auth/login-code", new { email = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, account.Email.Value, Ct);
        using var verified = await AccountJourney.PostAsync(browser, "/api/auth/login-code/verify", new
        {
            email = account.Email.Value, code,
            returnUrl = AuthorizePath("https://localhost:5174/auth/callback", "business", tenantId),
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
    }

    private static string PublicOrigin(string slug) => $"https://{slug}.localhost:5174";

    private static string AuthorizePath(string redirect, string access = "consumer", Guid? tenantId = null) =>
        QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "web", ["response_type"] = "code", ["redirect_uri"] = redirect,
            ["scope"] = "openid profile email api offline_access", ["access"] = access,
            ["tenant"] = tenantId?.ToString("D"), ["code_challenge_method"] = "S256",
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier))),
        });

    private static async Task<SubdomainTokens> AuthorizeAndExchangeAsync(HttpClient browser, string origin,
        string access = "consumer", Guid? tenantId = null, bool silent = false)
    {
        var redirect = origin + "/auth/callback";
        using var response = await browser.GetAsync(AuthorizePath(redirect, access, tenantId) + (silent ? "&prompt=none" : string.Empty), Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var parameters = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.True(parameters.ContainsKey("code"), "La autorización no entregó un código OIDC.");
        return await ExchangeAsync(browser, origin, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = "web", ["code"] = parameters["code"].ToString(),
            ["redirect_uri"] = redirect, ["code_verifier"] = Verifier,
        });
    }

    private static async Task<SubdomainTokens> ExchangeAsync(HttpClient browser, string origin, Dictionary<string, string> form)
    {
        using var response = await browser.PostAsync(origin + "/connect/token", new FormUrlEncodedContent(form), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return new SubdomainTokens(body!.RootElement.GetProperty("access_token").GetString()!,
            body.RootElement.GetProperty("refresh_token").GetString()!, body.RootElement.GetProperty("id_token").GetString()!);
    }

    private static async Task<HttpResponseMessage> UserInfoAsync(HttpClient browser, string origin, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, origin + "/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await browser.SendAsync(request, Ct);
    }

    private static JsonDocument ReadClaims(string idToken) => JsonDocument.Parse(WebEncoders.Base64UrlDecode(idToken.Split('.')[1]));

    private sealed record SubdomainTokens(string AccessToken, string RefreshToken, string IdToken)
    {
        public override string ToString() => nameof(SubdomainTokens);
    }

    private async Task<Tenant> CreateSiteAsync(TenantStatus status = TenantStatus.Active,
        bool published = true, bool blocked = false, bool withPage = true, string? slug = null, Guid? ownerId = null)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var tenant = Tenant.CreateBusiness("Organización pública", status == TenantStatus.PendingApproval);
        Assert.True(tenant.SetSlug(TenantSlug.Create(slug ?? "site-" + Guid.NewGuid().ToString("N")).Value).IsSuccess);
        if (status is TenantStatus.Active or TenantStatus.Suspended or TenantStatus.Closed)
            Assert.True(tenant.Activate().IsSuccess);
        if (status is TenantStatus.Suspended or TenantStatus.Closed) Assert.True(tenant.Suspend().IsSuccess);
        if (status == TenantStatus.Closed) Assert.True(tenant.Close().IsSuccess);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant.Id, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            if (ownerId is { } userId)
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
            else services.GetRequiredService<ITenantRepository>().Add(tenant);
            if (withPage)
            {
                var page = PublicPage.Create();
                Assert.True(page.UpdateContent(tenant.Name, null, null, null, null, null).IsSuccess);
                var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                if (published) Assert.True(page.Publish(nowUtc).IsSuccess);
                if (blocked) Assert.True(page.ApplyModerationBlock("Moderación", Guid.NewGuid(), nowUtc).IsSuccess);
                services.GetRequiredService<IPublicPageRepository>().Add(page);
            }
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return tenant;
    }

    private static async Task AssertNotFoundAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString(),
            StringComparison.OrdinalIgnoreCase);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(PublicPageErrors.NotFoundCode, body!.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    private HttpClient Client() => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private static string SiteUrl(string slug) => $"https://{slug}.localhost:5174/test/access/public-site";

    private static DefaultHttpContext PublicRequest(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PublicSiteAttribute()), "public-site"));
        return context;
    }

    private sealed class StubOrigin(Uri value) : IPublicOrigin
    {
        public Uri? Value { get; } = value;
    }

    private sealed class RecordingReader : IPublicSiteReader
    {
        public int Calls { get; private set; }

        public Task<PublicSiteRow?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<PublicSiteRow?>(new PublicSiteRow(Guid.NewGuid(), normalizedSlug));
        }
    }
}
