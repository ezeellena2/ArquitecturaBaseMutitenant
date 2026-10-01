using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba que xmin detecte una edición simultánea y preserve el primer cambio. La API debe informar el
/// conflicto con status 409 y código estable.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ConcurrencyTests(ApiFactory factory)
{
    [Fact]
    public async Task Second_edit_with_stale_xmin_returns_conflict_without_overwriting_first()
    {
        var tenantId = Guid.NewGuid();
        var widget = new Widget(tenantId, "Original");
        await using (var setup = CreateContext(tenantId))
        {
            await SaveAsync(setup, ct =>
            {
                setup.Set<Widget>().Add(widget);
                return Task.FromResult(Result.Success());
            });
        }

        await using var first = CreateContext(tenantId);
        await using var second = CreateContext(tenantId);
        var firstCopy = await first.Set<Widget>().SingleAsync(
            item => item.Id == widget.Id, TestContext.Current.CancellationToken);
        var secondCopy = await second.Set<Widget>().SingleAsync(
            item => item.Id == widget.Id, TestContext.Current.CancellationToken);
        Assert.Equal(firstCopy.Version, secondCopy.Version);

        first.Entry(firstCopy).Property(nameof(Widget.Name)).CurrentValue = "Primera edición";
        await SaveAsync(first, _ => Task.FromResult(Result.Success()));

        second.Entry(secondCopy).Property(nameof(Widget.Name)).CurrentValue = "Edición vencida";
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            SaveAsync(second, _ => Task.FromResult(Result.Success())));

        await using var verify = CreateContext(tenantId);
        var persisted = await verify.Set<Widget>().SingleAsync(
            item => item.Id == widget.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Primera edición", persisted.Name);
    }

    [Fact]
    public async Task Api_returns_409_with_stable_code_for_concurrent_edit()
    {
        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/test/widgets/{Guid.NewGuid()}",
            new WidgetUpdateHttpRequest("Nuevo nombre", 1),
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("General.ConcurrencyConflict", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Edit_without_version_is_rejected_before_the_action()
    {
        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/test/widgets/{Guid.NewGuid()}",
            new { name = "Sin versión" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private IsolationApplicationDbContext CreateContext(Guid tenantId)
    {
        var tenant = Tenant(tenantId);
        var options = new DbContextOptionsBuilder<IsolationApplicationDbContext>()
            .UseNpgsql(factory.RuntimeConnectionString)
            .ReplaceService<IModelCustomizer, IsolationModelCustomizer>()
            .AddInterceptors(
                new TenantConnectionInterceptor(tenant),
                new AuditableEntityInterceptor(new SystemCurrentUser(), TimeProvider.System))
            .Options;
        return new IsolationApplicationDbContext(options, tenant);
    }

    private static async Task<Result> SaveAsync(
        IsolationApplicationDbContext context,
        Func<CancellationToken, Task<Result>> work)
    {
        var unitOfWork = new UnitOfWork(
            context, Tenant(context.ActiveTenantId ?? throw new InvalidOperationException("No active tenant.")),
            NullLogger<UnitOfWork>.Instance);
        return await unitOfWork.ExecuteInTransactionAsync(
            work, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken);
    }

    private static TenantContext Tenant(Guid id)
    {
        var tenant = new TenantContext();
        tenant.SetFromAccess(id, TenantKind.Personal);
        return tenant;
    }
}
