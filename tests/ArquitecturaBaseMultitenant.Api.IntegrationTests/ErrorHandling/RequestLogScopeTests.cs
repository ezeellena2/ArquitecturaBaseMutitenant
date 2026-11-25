using ArquitecturaBaseMultitenant.Api.RequestContext;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.ErrorHandling;

/// <summary>
/// N10: los logs de un pedido con sesión llevan el id de quien pide y el de su organización en un scope, que la consola
/// JSON escribe junto al traceId; el manejador de errores abre el mismo scope para el log del 500. Sin sesión no hay
/// scope, y nunca va el correo ni el número.
/// </summary>
public sealed class RequestLogScopeTests
{
    private static readonly Action<ILogger, Exception?> Logged =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "Logged"), "Logged inside the request.");

    [Fact]
    public async Task A_request_in_an_organization_logs_the_person_and_the_organization()
    {
        var (userId, tenantId) = (Guid.NewGuid(), Guid.NewGuid());

        var scope = await LogInsideAsync(userId, tenantId);

        var values = Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(Assert.Single(scope));
        Assert.Contains(values, pair => pair is { Key: "UserId" } && Equals(pair.Value, userId));
        Assert.Contains(values, pair => pair is { Key: "TenantId" } && Equals(pair.Value, tenantId));
    }

    [Fact]
    public async Task A_request_without_organization_logs_only_the_person()
    {
        var userId = Guid.NewGuid();

        var scope = await LogInsideAsync(userId, tenantId: null);

        var values = Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(Assert.Single(scope));
        Assert.Contains(values, pair => pair is { Key: "UserId" } && Equals(pair.Value, userId));
        Assert.DoesNotContain(values, pair => pair.Key == "TenantId");
    }

    [Fact]
    public async Task An_anonymous_request_opens_no_scope()
    {
        Assert.Empty(await LogInsideAsync(userId: null, tenantId: null));
    }

    [Fact]
    public void The_error_handler_scope_carries_the_same_ids()
    {
        var (userId, tenantId) = (Guid.NewGuid(), Guid.NewGuid());
        var logger = new FakeLogger<RequestLogScopeTests>();

        using (RequestLogScopeMiddleware.Begin(logger, Context(userId, tenantId)))
        {
            Logged(logger, null);
        }

        var values = Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(Assert.Single(logger.LatestRecord.Scopes));
        Assert.Contains(values, pair => pair is { Key: "TenantId" } && Equals(pair.Value, tenantId));
    }

    private static async Task<IReadOnlyList<object?>> LogInsideAsync(Guid? userId, Guid? tenantId)
    {
        var logger = new FakeLogger<RequestLogScopeMiddleware>();
        var middleware = new RequestLogScopeMiddleware(_ =>
        {
            Logged(logger, null);
            return Task.CompletedTask;
        }, logger);

        await middleware.InvokeAsync(Context(userId, tenantId));

        return logger.LatestRecord.Scopes;
    }

    private static DefaultHttpContext Context(Guid? userId, Guid? tenantId) => new()
    {
        RequestServices = new ServiceCollection()
            .AddSingleton<ICurrentUser>(new FixedUser(userId))
            .AddSingleton<ITenantContext>(new FixedTenant(tenantId))
            .BuildServiceProvider(),
    };

    private sealed class FixedUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public Access? Access => userId is null ? null : Domain.Users.Access.Business;
    }

    private sealed class FixedTenant(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => tenantId ?? throw new InvalidOperationException("No tenant.");
    }
}
