using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
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
using MimeKit;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class ConnectTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
            builder.UseEnvironment("Development");
            builder.UseSetting("Email:Delivery", "PickupDirectory");
            builder.UseSetting("Authentication:Google:ClientId", "");
            builder.UseSetting("Seed:Development:AnaEmail", email);
        });
        await host.Services.SeedDatabaseAsync(Ct);
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

    private static async Task<HttpResponseMessage> PostOnceAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private static async Task<string> ReadPickupCodeAsync(IServiceProvider provider, string email)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var directory = Path.Combine(services.GetRequiredService<IHostEnvironment>().ContentRootPath, ".emails");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await services.GetRequiredService<IOutboxDispatchService>().DispatchOnceAsync(
                services.GetServices<IChannelSender>().ToArray(), Ct);
            foreach (var path in Directory.Exists(directory) ? Directory.GetFiles(directory, "*.eml") : [])
            {
                string? code;
                await using (var stream = File.OpenRead(path))
                {
                    using var message = await MimeMessage.LoadAsync(stream, Ct);
                    if (!message.To.Mailboxes.Any(mailbox => mailbox.Address == email)) continue;
                    var match = Regex.Match(message.TextBody ?? string.Empty, @"(?<!\d)\d{6}(?!\d)");
                    Assert.True(match.Success, "El correo pickup no contiene un código de seis dígitos.");
                    code = match.Value;
                }
                File.Delete(path);
                return code;
            }
            await Task.Delay(100, Ct);
        }
        throw new Xunit.Sdk.XunitException("No se encontró el correo pickup de ingreso.");
    }

    private static string AuthorizePath(string access) =>
        "/connect/authorize?client_id=web&response_type=code" +
        "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback" +
        "&scope=openid%20profile%20email%20api" +
        "&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "&code_challenge_method=S256&access=" + access;
}
