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

/// <summary>
/// Hospeda la API de integración sobre un PostgreSQL aislado de Testcontainers.
/// Prepara roles, esquema de aislamiento y correo pickup para probar HTTP, persistencia y RLS sin usar Development.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgres;
    private readonly IsolatedDatabaseConnections? _external;
    private readonly string _ownerPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string _runtimePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string _pickupDirectory = Path.Combine(Path.GetFullPath(Path.GetTempPath()),
        $"mt-tests-{Guid.NewGuid():N}");

    public ApiFactory()
    {
        _postgres = new PostgreSqlBuilder("postgres:18.3").WithDatabase("postgres").Build();
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    public static ApiFactory ForIsolatedDatabase(IsolatedDatabaseConnections external) => new(external);

    private ApiFactory(IsolatedDatabaseConnections external)
    {
        ArgumentNullException.ThrowIfNull(external);
        external.Validate();
        _external = external;
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    public string BootstrapConnectionString => _external?.Bootstrap ?? new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString())
    {
        Database = "postgres",
    }.ConnectionString;

    public string AdminConnectionString => _external?.Admin ?? RoleConnectionString("mt_owner", _ownerPassword);

    public string RuntimeConnectionString => _external?.Runtime ?? RoleConnectionString("mt_app", _runtimePassword);

    public string ConnectionString => RuntimeConnectionString;

    public ValueTask InitializeAsync() => InitializeDatabaseAsync(TestContext.Current.CancellationToken);

    public async ValueTask InitializeDatabaseAsync(CancellationToken ct)
    {
        if (_postgres is not null) await _postgres.StartAsync(ct);
        await DatabaseBootstrapExtensions.BootstrapAsync(
            BootstrapConnectionString,
            AdminConnectionString,
            RuntimeConnectionString,
            ct);

        if (_postgres is not null)
        {
            await using var connection = new NpgsqlConnection(AdminConnectionString);
            await IsolationSchema.ApplyAsync(connection, ct);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
            if (_postgres is not null) await _postgres.DisposeAsync();
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
        // Aísla los hosts Development de Phone personal; una fixture puede sobrescribirlo explícitamente.
        builder.UseSetting("Seed:PlatformOwner:Phone", string.Empty);
        builder.UseSetting("Email:Delivery", "PickupDirectory");
        builder.UseSetting("WhatsApp:PhoneNumberId", string.Empty);
        builder.UseSetting("Authentication:Google:ClientId", string.Empty);
        builder.UseSetting("Authentication:Google:ClientSecret", string.Empty);
        // Como Development: cada registro se construye al arrancar, así una dependencia sin registrar rompe la suite
        // entera. ValidateScopes queda apagado porque hay pruebas que leen servicios scoped desde la raíz.
        builder.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);
        builder.UseSetting("ConnectionStrings:postgres-bootstrap", BootstrapConnectionString);
        builder.UseSetting("ConnectionStrings:appdb-admin", AdminConnectionString);
        builder.UseSetting("ConnectionStrings:appdb", RuntimeConnectionString);
        builder.UseSetting("Authentication:Issuer", "https://localhost:5174/");
        builder.UseSetting("Authentication:LoginCode:HashKey",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        // La suite comparte la IP del TestServer entre casos; las pruebas de rate limit
        // configuran límites bajos explícitamente en sus propias factories.
        builder.UseSetting("RateLimiting:LoginCodePermitLimit", "1000");
        builder.UseSetting("RateLimiting:LoginVerifyPermitLimit", "1000");
        builder.UseSetting("Email:PickupDirectory", _pickupDirectory);
        builder.UseSetting("Authentication:Clients:Web:RedirectUris:0", "https://localhost:5174/auth/callback");
        builder.UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:0", "https://localhost:5174/");
        builder.UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:1", "https://localhost:5174/invitacion");
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        if (_external is null)
            builder.ConfigureTestServices(services =>
                services.AddControllers().ConfigureApplicationPartManager(parts =>
                    parts.ApplicationParts.Add(new TestControllerApplicationPart())));
    }

    private string RoleConnectionString(string role, string password) =>
        new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString())
        {
            Database = "appdb",
            Username = role,
            Password = password,
        }.ConnectionString;
}
