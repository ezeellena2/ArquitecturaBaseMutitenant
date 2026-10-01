using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que los workers recorran empresas activas y entren al alcance antes del trabajo. Incluye
/// invalidación de caché al cambiar el estado.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class TenantJobRunnerIdentityTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Worker_runs_only_active_businesses_and_enters_scope_before_job()
    {
        using var client = factory.CreateClient();
        await using var setupScope = factory.Services.CreateAsyncScope();
        var setup = setupScope.ServiceProvider;
        var active = Tenant.CreateBusiness("Job active", requiresApproval: false);
        var suspended = Tenant.CreateBusiness("Job suspended", requiresApproval: false);
        var personal = Tenant.CreatePersonal("Job personal");
        Assert.True(active.Activate().IsSuccess);
        Assert.True(suspended.Activate().IsSuccess);
        Assert.True(suspended.Suspend().IsSuccess);

        await setup.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            var repository = setup.GetRequiredService<ITenantRepository>();
            repository.Add(active);
            repository.Add(suspended);
            repository.Add(personal);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var seen = new List<Guid>();
        var runner = factory.Services.GetRequiredService<TenantJobRunner>();
        await runner.RunActiveBusinessesAsync((services, ct) =>
        {
            var tenantId = services.GetRequiredService<ITenantContext>().RequiredTenantId;
            Assert.Null(services.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction);
            seen.Add(tenantId);
            return Task.CompletedTask;
        }, Ct);

        Assert.Contains(active.Id, seen);
        Assert.DoesNotContain(suspended.Id, seen);
        Assert.DoesNotContain(personal.Id, seen);
    }

    [Fact]
    public async Task Tenant_status_cache_is_invalidated_after_status_change()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tenant = Tenant.CreateBusiness("Cached status", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var cache = services.GetRequiredService<ITenantStatusCache>();
        Assert.Equal(TenantStatus.Active, await cache.GetStatusAsync(tenant.Id, Ct));

        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var tracked = await services.GetRequiredService<ITenantRepository>().GetByIdAsync(tenant.Id, ct);
            Assert.NotNull(tracked);
            Assert.True(tracked.Suspend().IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        await cache.InvalidateAsync(tenant.Id, Ct);

        Assert.Equal(TenantStatus.Suspended, await cache.GetStatusAsync(tenant.Id, Ct));
    }
}
