using System.Security.Claims;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

public sealed class SuspensionTests
{
    [Theory]
    [InlineData(TenantStatus.Suspended, TenantErrors.SuspendedCode)]
    [InlineData(TenantStatus.PendingApproval, TenantErrors.PendingApprovalCode)]
    [InlineData(TenantStatus.Closed, TenantErrors.ClosedCode)]
    public async Task Inactive_business_returns_its_state_code_without_setting_context(
        TenantStatus status, string expectedCode)
    {
        var tenantId = Guid.NewGuid();
        var context = NewContext(Access.Business, tenantId, TenantKind.Business);
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(status), initializer);

        await middleware.InvokeAsync(context, ActiveAccessCache(), initializer, new StubTenantReader());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Null(initializer.TenantId);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expectedCode, body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Empresa A", body.RootElement.GetProperty("organizationName").GetString());
        Assert.Equal(tenantId.ToString("D"), body.RootElement.GetProperty("tenantId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Active_business_sets_tenant_from_claim_and_continues()
    {
        var tenantId = Guid.NewGuid();
        var context = NewContext(Access.Business, tenantId, TenantKind.Business);
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Active), initializer);

        await middleware.InvokeAsync(context, ActiveAccessCache(), initializer, new StubTenantReader());

        Assert.Equal(tenantId, initializer.TenantId);
        Assert.Equal(TenantKind.Business, initializer.Kind);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invalid_tenant_claim_never_falls_back_to_host()
    {
        var context = NewContext(Access.Business, null, null);
        context.Request.Host = new HostString("empresa.example.test");
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Active), initializer);

        await middleware.InvokeAsync(context, ActiveAccessCache(), initializer, new StubTenantReader());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(initializer.TenantId);
    }

    [Fact]
    public async Task Anonymous_route_does_not_resolve_a_tenant()
    {
        var context = NewContext(null, null, null);
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Active), initializer);

        await middleware.InvokeAsync(context, ActiveAccessCache(), initializer, new StubTenantReader());

        Assert.Null(initializer.TenantId);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_endpoint_ignores_bearer_from_inactive_business()
    {
        var context = NewContext(Access.Business, Guid.NewGuid(), TenantKind.Business);
        context.Request.Path = "/api/reference-data";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()), "reference-data"));
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Suspended), initializer);

        await middleware.InvokeAsync(context,
            new StubAccessStatusCache(UserStatus.Active, MemberStatus.Inactive), initializer,
            new StubTenantReader());

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Null(initializer.TenantId);
    }

    [Fact]
    public async Task Anonymous_endpoint_ignores_bearer_from_inactive_identity()
    {
        var context = NewContext(Access.Business, Guid.NewGuid(), TenantKind.Business);
        context.Request.Path = "/api/legal/terms";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()), "legal"));
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Active), initializer);

        await middleware.InvokeAsync(context, new StubAccessStatusCache(UserStatus.Suspended), initializer,
            new StubTenantReader());

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Null(initializer.TenantId);
    }

    [Fact]
    public async Task Identity_cookie_without_access_claim_continues_to_connect_route()
    {
        var context = NewContext(null, null, null);
        context.Request.Path = "/connect/authorize";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString("D"))], "Identity.Application"));
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Active), initializer);

        await middleware.InvokeAsync(context, ActiveAccessCache(), initializer, new StubTenantReader());

        Assert.Null(initializer.TenantId);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task Suspended_identity_cannot_continue_even_with_active_tenant()
    {
        var context = NewContext(Access.Business, Guid.NewGuid(), TenantKind.Business);
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Active), initializer);

        await middleware.InvokeAsync(context, new StubAccessStatusCache(UserStatus.Suspended), initializer,
            new StubTenantReader());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Null(initializer.TenantId);
    }

    [Fact]
    public async Task Inactive_membership_cannot_continue_even_with_active_tenant()
    {
        var context = NewContext(Access.Business, Guid.NewGuid(), TenantKind.Business);
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Active), initializer);

        await middleware.InvokeAsync(context,
            new StubAccessStatusCache(UserStatus.Active, MemberStatus.Inactive), initializer,
            new StubTenantReader());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(initializer.TenantId);
    }

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Closed)]
    public async Task Get_me_remains_available_to_switch_away_from_inactive_business(TenantStatus status)
    {
        var tenantId = Guid.NewGuid();
        var context = NewContext(Access.Business, tenantId, TenantKind.Business);
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/me";
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(status), initializer);

        await middleware.InvokeAsync(context,
            new StubAccessStatusCache(UserStatus.Active, MemberStatus.Inactive), initializer,
            new StubTenantReader());

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(tenantId, initializer.TenantId);
    }

    [Fact]
    public async Task Post_me_remains_forbidden_for_inactive_business()
    {
        var context = NewContext(Access.Business, Guid.NewGuid(), TenantKind.Business);
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/me";
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Suspended), initializer);

        await middleware.InvokeAsync(context, ActiveAccessCache(), initializer, new StubTenantReader());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(initializer.TenantId);
    }

    [Fact]
    public async Task Connect_logout_remains_available_for_inactive_business()
    {
        var context = NewContext(Access.Business, Guid.NewGuid(), TenantKind.Business);
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/connect/logout";
        var initializer = new RecordingInitializer();
        var middleware = NewMiddleware(new StubStatusCache(TenantStatus.Suspended), initializer);

        await middleware.InvokeAsync(context, ActiveAccessCache(), initializer, new StubTenantReader());

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    private static DefaultHttpContext NewContext(Access? access, Guid? tenantId, TenantKind? kind)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddProblemDetails();
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        if (access is { } selected)
        {
            var claims = new List<Claim>
            {
                new("sub", Guid.NewGuid().ToString("D")),
                new(TenantClaimTypes.Access, selected.ToString().ToLowerInvariant()),
            };
            if (tenantId is { } id) claims.Add(new Claim(TenantClaimTypes.TenantId, id.ToString("D")));
            if (kind is { } tenantKind) claims.Add(new Claim(TenantClaimTypes.TenantKind, tenantKind.ToString().ToLowerInvariant()));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        return context;
    }

    private static TenantResolutionMiddleware NewMiddleware(ITenantStatusCache cache, ITenantAccessInitializer initializer) =>
        new(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            await Task.CompletedTask;
        }, cache);

    private static StubAccessStatusCache ActiveAccessCache() => new();

    private sealed class RecordingInitializer : ITenantAccessInitializer
    {
        public Guid? TenantId { get; private set; }
        public TenantKind? Kind { get; private set; }

        public void SetFromAccess(Guid tenantId, TenantKind kind)
        {
            TenantId = tenantId;
            Kind = kind;
        }
    }

    private sealed class StubStatusCache(TenantStatus? status) : ITenantStatusCache
    {
        public Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(status);

        public ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class StubTenantReader : ITenantReader
    {
        public Task<TenantRow?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantRow?>(new TenantRow(tenantId, TenantKind.Business,
                TenantStatus.Suspended, "Empresa A"));

        public Task<IReadOnlyList<TenantRow>> ListActiveBusinessesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantRow>>([]);
    }

    private sealed class StubAccessStatusCache(
        UserStatus userStatus = UserStatus.Active, MemberStatus memberStatus = MemberStatus.Active) : IAccessStatusCache
    {
        public Task<UserStatus?> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserStatus?>(userStatus);

        public Task<MemberStatus?> GetMemberStatusAsync(Guid userId, Guid tenantId, TenantKind kind,
            CancellationToken cancellationToken) => Task.FromResult<MemberStatus?>(memberStatus);

        public ValueTask InvalidateUserAsync(Guid userId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask InvalidateMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
