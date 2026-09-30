using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class ConnectTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]
    [InlineData("&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&code_challenge_method=plain")]
    public async Task Authorize_rejects_missing_or_plain_pkce(string pkceQuery)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var path = "/connect/authorize?client_id=web&response_type=code" +
            "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback" +
            "&scope=openid%20profile%20email%20api&access=consumer" + pkceQuery;

        using var response = await client.GetAsync(path, Ct);

        var result = response.Headers.Location?.ToString() ??
            await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("invalid_request", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/login", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("consumer", "/login")]
    [InlineData("business", "/login/empresa")]
    public async Task Authorize_without_cookie_preserves_the_selected_access_and_original_request(
        string access, string loginPath)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(AuthorizePath(access), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith(loginPath + "?returnUrl=", location, StringComparison.Ordinal);
        var decoded = Uri.UnescapeDataString(location);
        Assert.Contains("/connect/authorize?", decoded, StringComparison.Ordinal);
        Assert.Contains("access=", decoded, StringComparison.Ordinal);
        Assert.Contains(access, decoded, StringComparison.Ordinal);
        Assert.Contains("client_id=web", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Silent_authorize_without_cookie_returns_login_required_without_opening_the_login_page()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(AuthorizePath("business") + "&prompt=none", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=login_required", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cookie_session_with_inactive_business_returns_the_specific_state_and_name()
    {
        var email = $"ana-connect-{Guid.NewGuid():N}@example.test";
        await using var isolatedFactory = new ApiFactory();
        await isolatedFactory.InitializeAsync();
        using var host = isolatedFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Email:Delivery", "PickupDirectory");
            builder.UseSetting("Authentication:Google:ClientId", "");
        });
        await SampleAccountsFixture.SeedAsync(host.Services, email, Ct);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        using var requested = await PostOnceAsync(client, "/api/auth/login-code", new { email });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await ReadPickupCodeAsync(host.Services, email);
        using var verified = await PostOnceAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = AuthorizePath("business") });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("Identity.Application", StringComparison.Ordinal));

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var method = await services.GetRequiredService<IUserLookup>()
                .FindMethodAsync(LoginMethodType.Email, email, Ct);
            Assert.NotNull(method);
            var context = services.GetRequiredService<ApplicationDbContext>();
            var tenantId = await context.Tenants.AsNoTracking()
                .Where(tenant => tenant.Name == "Empresa A" && tenant.Kind == TenantKind.Business)
                .Select(tenant => tenant.Id).SingleAsync(Ct);
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var deactivated = await services.GetRequiredService<IUnitOfWork>()
                .ExecuteInTransactionAsync(async ct =>
                {
                    var member = await context.Members.SingleAsync(item => item.UserId == method.UserId, ct);
                    return member.Deactivate();
                }, CommitPolicy.OnSuccess, Ct);
            Assert.True(deactivated.IsSuccess);
            await services.GetRequiredService<IAccessStatusCache>()
                .InvalidateMemberAsync(method.UserId, tenantId, Ct);
        }

        using var denied = await client.GetAsync(AuthorizePath("business"), Ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        var location = denied.Headers.Location;
        Assert.NotNull(location);
        var parameters = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("access_denied", parameters["error"].ToString());
        Assert.Equal("Tenancy.Member.Inactive|Empresa%20A", parameters["error_description"].ToString());
        Assert.False(parameters.ContainsKey("code"));
    }

    [Fact]
    public async Task Cookie_session_cannot_escalate_to_platform_or_select_a_foreign_tenant()
    {
        var email = $"ana-invalid-access-{Guid.NewGuid():N}@example.test";
        await using var isolatedFactory = new ApiFactory();
        await isolatedFactory.InitializeAsync();
        using var host = isolatedFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Email:Delivery", "PickupDirectory");
            builder.UseSetting("Authentication:Google:ClientId", "");
        });
        await SampleAccountsFixture.SeedAsync(host.Services, email, Ct);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        using var requested = await PostOnceAsync(client, "/api/auth/login-code", new { email });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await ReadPickupCodeAsync(host.Services, email);
        using var verified = await PostOnceAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = AuthorizePath("business") });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        var foreignTenant = Guid.CreateVersion7();
        foreach (var path in new[]
        {
            AuthorizePath("platform"),
            AuthorizePath("business") + "&tenant=" + foreignTenant.ToString("D"),
            AuthorizePath("consumer") + "&tenant=" + foreignTenant.ToString("D"),
        })
        {
            using var denied = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            var location = denied.Headers.Location;
            Assert.NotNull(location);
            var parameters = QueryHelpers.ParseQuery(location.Query);
            Assert.Equal("access_denied", parameters["error"].ToString());
            Assert.False(parameters.ContainsKey("code"));
        }
    }

    private static async Task<HttpResponseMessage> PostOnceAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private static Task<string> ReadPickupCodeAsync(IServiceProvider provider, string email) =>
        PickupCodeReader.ReadAsync(provider, email, Ct);

    private static string AuthorizePath(string access) =>
        "/connect/authorize?client_id=web&response_type=code" +
        "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback" +
        "&scope=openid%20profile%20email%20api" +
        "&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "&code_challenge_method=S256&access=" + access;
}
