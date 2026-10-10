using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

internal static class OutboxPrivacyFixture
{
    internal static readonly DateTime NowUtc = new(2030, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    internal static WebApplicationFactory<Program> Host(ApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(NowUtc)));
        }));

    internal static async Task TransactionAsync(IServiceProvider services, Func<IServiceProvider, CancellationToken, Task> work, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async c =>
        { await work(scope.ServiceProvider, c); return Result.Success(); }, CommitPolicy.OnSuccess, ct);
        Assert.True(result.IsSuccess);
    }
}
