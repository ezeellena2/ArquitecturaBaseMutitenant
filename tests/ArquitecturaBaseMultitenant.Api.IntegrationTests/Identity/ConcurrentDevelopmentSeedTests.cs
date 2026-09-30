using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class ConcurrentDevelopmentSeedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_seeds_create_each_development_space_once()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.3")
            .WithDatabase("postgres")
            .Build();
        await postgres.StartAsync(Ct);
        var bootstrap = postgres.GetConnectionString();
        var ownerPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var runtimePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var admin = RoleConnection("mt_owner", ownerPassword);
        var runtime = RoleConnection("mt_app", runtimePassword);
        await DatabaseBootstrapExtensions.BootstrapAsync(bootstrap, admin, runtime, Ct);

        var gate = new SeedStartGate();
        using var firstHost = NewHost(1);
        using var secondHost = NewHost(2);
        var firstStart = Task.Run(() => firstHost.CreateClient(), Ct);
        var secondStart = Task.Run(() => secondHost.CreateClient(), Ct);
        using var firstClient = await firstStart;
        using var secondClient = await secondStart;
        Assert.Equal(2, gate.Arrivals);

        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync(Ct);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM platform.\"Tenants\" WHERE \"Kind\"='Business'", connection));
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM platform.\"Tenants\" WHERE \"Kind\"='Personal'", connection));
        Assert.Equal(3, await CountAsync("SELECT count(*) FROM identity.\"AspNetUsers\"", connection));
        Assert.Equal(3, await CountAsync("SELECT count(*) FROM identity.\"LoginMethods\"", connection));
        Assert.Equal(4, await CountAsync("SELECT count(*) FROM identity.\"UserTenantAccesses\"", connection));

        WebApplicationFactory<Program> NewHost(int replica) =>
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:postgres-bootstrap", bootstrap);
                builder.UseSetting("ConnectionStrings:appdb-admin", admin);
                builder.UseSetting("ConnectionStrings:appdb", runtime);
                builder.UseSetting("Authentication:Issuer", "https://example.test/");
                builder.UseSetting("Authentication:Clients:Web:RedirectUris:0", "https://example.test/auth/callback");
                builder.UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:0", "https://example.test/");
                builder.UseSetting("Email:Delivery", "PickupDirectory");
                builder.UseSetting("Seed:Development:AnaEmail", "ana@example.test");
                builder.ConfigureTestServices(services =>
                {
                    var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IUnitOfWork));
                    services.Remove(descriptor);
                    services.AddScoped<IUnitOfWork>(provider => new GatedUnitOfWork(
                        (IUnitOfWork)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!),
                        gate, replica));
                });
            });

        string RoleConnection(string user, string password) => new NpgsqlConnectionStringBuilder(bootstrap)
        {
            Database = "appdb",
            Username = user,
            Password = password,
        }.ConnectionString;
    }

    private static async Task<long> CountAsync(string sql, NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private sealed class SeedStartGate
    {
        private readonly HashSet<int> _arrived = [];
        private readonly TaskCompletionSource _bothArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Arrivals
        {
            get
            {
                lock (_arrived) return _arrived.Count;
            }
        }

        public async Task ArriveAsync(int replica, CancellationToken cancellationToken)
        {
            lock (_arrived)
            {
                _arrived.Add(replica);
                if (_arrived.Count == 2) _bothArrived.TrySetResult();
            }
            await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private sealed class GatedUnitOfWork(IUnitOfWork inner, SeedStartGate gate, int replica) : IUnitOfWork
    {
        private int _started;

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
            CommitPolicy policy, CancellationToken cancellationToken) where TResult : Result
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) == 0)
                await gate.ArriveAsync(replica, cancellationToken);
            return await inner.ExecuteInTransactionAsync(work, policy, cancellationToken);
        }
    }
}
