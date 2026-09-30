using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class AuthPipelineTests(ApiFactory factory)
{
    internal static readonly Guid UserId = Guid.Parse("d8a8b23a-3e7c-4a60-a5ba-caa0928b4130");
    internal static readonly Guid TenantId = Guid.Parse("e8f8283c-f296-4fae-8564-05a129899909");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Authentication_resolves_the_signed_tenant_before_authorization()
    {
        await using var app = TestApp();
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/pipeline/authenticated");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PipelineProbe>(Ct);
        Assert.NotNull(result);
        Assert.Equal(UserId, result.UserId);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Equal(nameof(TenantKind.Business), result.TenantKind);
    }

    [Fact]
    public async Task Tenant_policy_rejects_before_tenant_resolution()
    {
        await using var app = TestApp();
        using var scope = app.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", UserId.ToString("D"))], PipelineAuthHandler.SchemeName));

        var result = await authorization.AuthorizeAsync(principal, resource: null, "TenantProbe");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Anonymous_route_has_no_tenant_context()
    {
        await using var app = TestApp();
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/test/pipeline/anonymous", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PipelineProbe>(Ct);
        Assert.NotNull(result);
        Assert.Null(result.UserId);
        Assert.Null(result.TenantId);
        Assert.Null(result.TenantKind);
    }

    [Fact]
    public async Task Authenticated_me_update_persists_culture_and_get_reads_it()
    {
        await using var app = TestApp(development: true);
        using var client = app.CreateClient();
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = """
            SELECT u."Id", a."TenantId"
            FROM identity."AspNetUsers" u
            JOIN identity."UserTenantAccesses" a ON a."UserId" = u."Id"
            JOIN platform."Tenants" t ON t."Id" = a."TenantId"
            WHERE u."DisplayName" = 'Carla' AND t."Kind" = 'Personal'
            """;
        await using var rows = await lookup.ExecuteReaderAsync(Ct);
        Assert.True(await rows.ReadAsync(Ct));
        var userId = rows.GetGuid(0);
        var tenantId = rows.GetGuid(1);
        await rows.DisposeAsync();

        using var put = AuthorizedRequest(HttpMethod.Put, "/api/me", userId, tenantId);
        put.Content = JsonContent.Create(new
        {
            displayName = "Carla",
            culture = "en-US",
            timeZoneId = "America/Argentina/Buenos_Aires",
        });
        using var updated = await client.SendAsync(put, Ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        using var get = AuthorizedRequest(HttpMethod.Get, "/api/me", userId, tenantId);
        using var read = await client.SendAsync(get, Ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var body = JsonDocument.Parse(await read.Content.ReadAsStringAsync(Ct));
        Assert.Equal("en-US", body.RootElement.GetProperty("culture").GetString());
        Assert.Equal("consumer", body.RootElement.GetProperty("access").GetString());
        Assert.Equal(userId, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(tenantId, body.RootElement.GetProperty("activeTenantId").GetGuid());
    }

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string path, Guid userId,
        Guid tenantId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        request.Headers.Add("X-Test-User-Id", userId.ToString("D"));
        request.Headers.Add("X-Test-Tenant-Id", tenantId.ToString("D"));
        return request;
    }

    private WebApplicationFactory<Program> TestApp(bool development = false) => factory.WithWebHostBuilder(builder =>
    {
        if (development)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Email:Delivery", "PickupDirectory");
            builder.UseSetting("Authentication:Google:ClientId", string.Empty);
        }
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = PipelineAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = PipelineAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, PipelineAuthHandler>(
                    PipelineAuthHandler.SchemeName, _ => { });
            services.RemoveAll<IAccessStatusCache>();
            services.RemoveAll<ITenantStatusCache>();
            services.AddSingleton<IAccessStatusCache, ActiveAccessStatusCache>();
            services.AddSingleton<ITenantStatusCache, ActiveTenantStatusCache>();
            services.AddAuthorization(options => options.AddPolicy("TenantProbe", policy =>
                policy.Requirements.Add(new TenantProbeRequirement())));
            services.AddScoped<IAuthorizationHandler, TenantProbeAuthorizationHandler>();
        });
    });

    private sealed class TenantProbeRequirement : IAuthorizationRequirement;

    private sealed class TenantProbeAuthorizationHandler(ITenantContext tenantContext)
        : AuthorizationHandler<TenantProbeRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            TenantProbeRequirement requirement)
        {
            if (tenantContext.TenantId == TenantId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class PipelineAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PipelineTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.Authorization.ToString().Equals("Test", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var userId = Guid.TryParse(Request.Headers["X-Test-User-Id"], out var fromHeaderUser)
                ? fromHeaderUser : UserId;
            var tenantId = Guid.TryParse(Request.Headers["X-Test-Tenant-Id"], out var fromHeaderTenant)
                ? fromHeaderTenant : TenantId;
            Claim[] claims =
            [
                new("sub", userId.ToString("D")),
                new(TenantClaimTypes.Access, Request.Headers.ContainsKey("X-Test-Tenant-Id")
                    ? "consumer" : "business"),
                new(TenantClaimTypes.TenantId, tenantId.ToString("D")),
                new(TenantClaimTypes.TenantKind, Request.Headers.ContainsKey("X-Test-Tenant-Id")
                    ? "personal" : "business"),
            ];
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class ActiveAccessStatusCache : IAccessStatusCache
    {
        public Task<UserStatus?> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserStatus?>(UserStatus.Active);

        public Task<MemberStatus?> GetMemberStatusAsync(Guid userId, Guid tenantId, TenantKind kind,
            CancellationToken cancellationToken) => Task.FromResult<MemberStatus?>(MemberStatus.Active);

        public ValueTask InvalidateUserAsync(Guid userId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask InvalidateMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private sealed class ActiveTenantStatusCache : ITenantStatusCache
    {
        public Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantStatus?>(TenantStatus.Active);

        public ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}

public sealed record PipelineProbe(Guid? UserId, Guid? TenantId, string? TenantKind);

[ApiController]
[Route("test/pipeline")]
public sealed class AuthPipelineProbeController(ITenantContext tenantContext) : ControllerBase
{
    [HttpGet("authenticated")]
    [Authorize(Policy = "TenantProbe")]
    [Access(Access.Business)]
    public ActionResult<PipelineProbe> Authenticated() => Ok(new PipelineProbe(
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId) ? userId : null,
        tenantContext.TenantId, tenantContext.TenantKind?.ToString()));

    [HttpGet("anonymous")]
    [AllowAnonymous]
    public ActionResult<PipelineProbe> Anonymous() => Ok(new PipelineProbe(null,
        tenantContext.TenantId, tenantContext.TenantKind?.ToString()));
}
