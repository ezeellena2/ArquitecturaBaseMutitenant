using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que cada organización reciba un alcance y contexto nuevos. Un fallo debe liberar su alcance
/// antes de continuar.
/// </summary>
public sealed class TenantJobRunnerTests
{
    [Fact]
    public async Task Every_tenant_gets_a_fresh_scope_and_only_its_own_context()
    {
        using var provider = CreateProvider();
        var runner = provider.GetRequiredService<TenantJobRunner>();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var seen = new List<(ITenantContext Context, Guid TenantId)>();

        await runner.RunAsync(ids, (scope, ct) =>
        {
            var context = scope.GetRequiredService<ITenantContext>();
            Assert.Equal(ids[seen.Count], context.RequiredTenantId);
            seen.Add((context, context.RequiredTenantId));
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(ids, seen.Select(item => item.TenantId));
        Assert.Equal(3, seen.Select(item => item.Context).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.All(seen, item => Assert.Null(item.Context.TenantId));
    }

    [Fact]
    public async Task A_failed_job_disposes_its_scope_before_the_next_run()
    {
        using var provider = CreateProvider();
        var runner = provider.GetRequiredService<TenantJobRunner>();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        ITenantContext? failedContext = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync([first], (scope, ct) =>
            {
                failedContext = scope.GetRequiredService<ITenantContext>();
                Assert.Equal(first, failedContext.RequiredTenantId);
                throw new InvalidOperationException("Expected test failure.");
            }, TestContext.Current.CancellationToken));

        Assert.NotNull(failedContext);
        Assert.Null(failedContext.TenantId);
        await runner.RunAsync([second], (scope, ct) =>
        {
            var context = scope.GetRequiredService<ITenantContext>();
            Assert.NotSame(failedContext, context);
            Assert.Equal(second, context.RequiredTenantId);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantScope>(provider => provider.GetRequiredService<TenantContext>());
        services.AddBackgroundJobs();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
