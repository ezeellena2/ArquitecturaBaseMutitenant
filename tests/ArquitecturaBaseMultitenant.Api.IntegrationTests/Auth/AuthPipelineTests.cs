using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    private WebApplicationFactory<Program> TestApp() => factory.WithWebHostBuilder(builder =>
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
        }));

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

            Claim[] claims =
            [
                new("sub", UserId.ToString("D")),
                new(TenantClaimTypes.Access, "business"),
                new(TenantClaimTypes.TenantId, TenantId.ToString("D")),
                new(TenantClaimTypes.TenantKind, "business"),
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
    [Authorize]
    [Access(Access.Business)]
    public ActionResult<PipelineProbe> Authenticated() => Ok(new PipelineProbe(
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId) ? userId : null,
        tenantContext.TenantId, tenantContext.TenantKind?.ToString()));

    [HttpGet("anonymous")]
    [AllowAnonymous]
    public ActionResult<PipelineProbe> Anonymous() => Ok(new PipelineProbe(null,
        tenantContext.TenantId, tenantContext.TenantKind?.ToString()));
}
