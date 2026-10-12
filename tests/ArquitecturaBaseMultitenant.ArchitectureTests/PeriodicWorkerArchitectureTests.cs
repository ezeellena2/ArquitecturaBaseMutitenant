using System.Reflection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class PeriodicWorkerArchitectureTests
{
    [Fact]
    public void Every_background_service_uses_the_common_worker_loop()
    {
        var workers = Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure").GetTypes()
            .Concat(Assembly.Load("ArquitecturaBaseMultitenant.Api").GetTypes())
            .Where(type => !type.IsAbstract && typeof(BackgroundService).IsAssignableFrom(type)).ToArray();
        Assert.NotEmpty(workers);
        Assert.Empty(workers.Where(type => !typeof(PeriodicWorker).IsAssignableFrom(type)).Select(type => type.FullName));
        Assert.All(workers, type => Assert.Equal(typeof(PeriodicWorker), type.GetMethod("ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!.DeclaringType));
    }
}
