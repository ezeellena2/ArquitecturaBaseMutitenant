using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class TenantConnectionTests(ApiFactory factory)
{
    [Fact]
    public async Task Pooled_connection_receives_each_scopes_tenant_and_clears_it_for_an_unscoped_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = new NpgsqlConnectionStringBuilder(factory.RuntimeConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 1,
            ApplicationName = $"tenant-connection-{Guid.NewGuid():N}",
        }.ConnectionString;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:appdb"] = connectionString,
            })
            .Build();
        await using var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddPersistence(configuration)
            .BuildServiceProvider();

        var firstTenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();
        var first = await ReadSessionAsync(services, firstTenant, cancellationToken);
        var second = await ReadSessionAsync(services, secondTenant, cancellationToken);
        var unscoped = await ReadSessionAsync(services, null, cancellationToken);

        Assert.Equal(first.BackendPid, second.BackendPid);
        Assert.Equal(second.BackendPid, unscoped.BackendPid);
        Assert.Equal(firstTenant, first.SessionTenant);
        Assert.Equal(secondTenant, second.SessionTenant);
        Assert.Null(unscoped.SessionTenant);
    }

    private static async Task<(int BackendPid, Guid? SessionTenant)> ReadSessionAsync(
        IServiceProvider services,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        using var tenantScope = tenantId is { } id
            ? scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(id)
            : null;
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_backend_pid(), current_setting('app.tenant_id', true)";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        var value = reader.IsDBNull(1) ? null : reader.GetString(1);
        return (reader.GetInt32(0), string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value));
    }
}
