using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using Testcontainers.PostgreSql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.3")
        .WithDatabase("postgres")
        .Build();
    private readonly string _ownerPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string _runtimePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string _pickupDirectory = Path.Combine(Path.GetFullPath(Path.GetTempPath()),
        $"mt-tests-{Guid.NewGuid():N}");

    public ApiFactory()
    {
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    public string BootstrapConnectionString => new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
    {
        Database = "postgres",
    }.ConnectionString;

    public string AdminConnectionString => RoleConnectionString("mt_owner", _ownerPassword);

    public string RuntimeConnectionString => RoleConnectionString("mt_app", _runtimePassword);

    public string ConnectionString => RuntimeConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await DatabaseBootstrapExtensions.BootstrapAsync(
            BootstrapConnectionString,
            AdminConnectionString,
            RuntimeConnectionString,
            TestContext.Current.CancellationToken);

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await IsolationSchema.ApplyAsync(connection, TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
            await _postgres.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(_pickupDirectory))
            {
                var target = Path.GetFullPath(_pickupDirectory);
                var parent = Path.GetDirectoryName(target);
                if (string.Equals(parent?.TrimEnd(Path.DirectorySeparatorChar),
                        Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(target).StartsWith("mt-tests-", StringComparison.Ordinal))
                    Directory.Delete(target, recursive: true);
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:postgres-bootstrap", BootstrapConnectionString);
        builder.UseSetting("ConnectionStrings:appdb-admin", AdminConnectionString);
        builder.UseSetting("ConnectionStrings:appdb", RuntimeConnectionString);
        builder.UseSetting("Authentication:Issuer", "https://localhost:5174/");
        builder.UseSetting("Authentication:LoginCode:HashKey",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Email:PickupDirectory", _pickupDirectory);
        builder.UseSetting("Authentication:Clients:Web:RedirectUris:0", "https://localhost:5174/auth/callback");
        builder.UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:0", "https://localhost:5174/");
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureTestServices(services =>
            services.AddControllers().ConfigureApplicationPartManager(parts =>
                parts.ApplicationParts.Add(new TestControllerApplicationPart())));
    }

    private string RoleConnectionString(string role, string password) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = "appdb",
            Username = role,
            Password = password,
        }.ConnectionString;
}
