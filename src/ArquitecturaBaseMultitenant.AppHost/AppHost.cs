using Aspire.Hosting.DevTunnels;
using System.Security.Cryptography;

// Orquesta Postgres, API y front para desarrollo; el recorrido E2E usa una base y correo pickup aislados.
var builder = DistributedApplication.CreateBuilder(args);

var isolatedE2e = Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") == "1";
// La corrida efímera nunca lee parámetros guardados de Development.
var postgresPassword = isolatedE2e
    ? builder.AddParameter("e2e-postgres-password", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), secret: true)
    : builder.AddParameter("postgres-password", secret: true);
var ownerPassword = isolatedE2e
    ? builder.AddParameter("e2e-owner-password", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), secret: true)
    : builder.AddParameter("mt-owner-password", secret: true);
var runtimePassword = isolatedE2e
    ? builder.AddParameter("e2e-app-password", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), secret: true)
    : builder.AddParameter("mt-app-password", secret: true);
var issuer = "https://localtest.me:5174/";

var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: isolatedE2e ? 5435 : 5434);
if (!isolatedE2e)
{
    postgres.WithDataVolume("arquitecturabase-multitenant-pgdata")
        .WithLifetime(ContainerLifetime.Persistent);
}

var endpoint = postgres.Resource.PrimaryEndpoint;
var host = endpoint.Property(EndpointProperty.Host);
var port = endpoint.Property(EndpointProperty.Port);
var postgresBootstrap = builder.AddConnectionString("postgres-bootstrap",
    ReferenceExpression.Create($"Host={host};Port={port};Database=postgres;Username=postgres;Password={postgresPassword}"));
var appDbAdmin = builder.AddConnectionString("appdb-admin",
    ReferenceExpression.Create($"Host={host};Port={port};Database=appdb;Username=mt_owner;Password={ownerPassword}"));
var appDb = builder.AddConnectionString("appdb",
    ReferenceExpression.Create($"Host={host};Port={port};Database=appdb;Username=mt_app;Password={runtimePassword}"));

var front = builder.AddViteApp("front", "../../../ArquitecturaBaseMutitenantFront")
    .WithNpm()
    .WithEnvironment("MT_E2E_ISOLATED", isolatedE2e ? "1" : "0")
    .WithEnvironment("VITE_AUTH_ISSUER", issuer)
    .WithHttpsEndpoint(port: 5174, env: "PORT", isProxied: false)
    .WithHttpsDeveloperCertificate();

if (isolatedE2e)
{
    var businessEmail = Environment.GetEnvironmentVariable("MT_E2E_BUSINESS_EMAIL")
        ?? throw new InvalidOperationException("MT_E2E_BUSINESS_EMAIL is required for the isolated E2E run.");
    var pickupDirectory = Environment.GetEnvironmentVariable("MT_E2E_PICKUP_DIR")
        ?? throw new InvalidOperationException("MT_E2E_PICKUP_DIR is required for the isolated E2E run.");
    var readyFile = Environment.GetEnvironmentVariable("MT_E2E_READY_FILE")
        ?? throw new InvalidOperationException("MT_E2E_READY_FILE is required for the isolated E2E run.");
    if (!Path.IsPathFullyQualified(pickupDirectory) || !Path.IsPathFullyQualified(readyFile))
        throw new InvalidOperationException("The isolated E2E paths must be absolute.");
    var setupProject = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "tools",
        "ArquitecturaBaseMultitenant.RealE2ESetup", "ArquitecturaBaseMultitenant.RealE2ESetup.csproj"));
    // La misma herramienta hospeda la API real Testing y prepara E7 en ese mismo IServiceProvider/base.
    var api = builder.AddExecutable("api", "dotnet", builder.AppHostDirectory,
            "run", "--project", setupProject, "--no-build", "--no-launch-profile", "--", "--recorrido-host")
        .WithReference(appDb).WithReference(appDbAdmin).WithReference(postgresBootstrap)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Testing")
        .WithEnvironment("DOTNET_ENVIRONMENT", "Testing")
        .WithEnvironment("MT_E2E_ISOLATED", "1")
        .WithEnvironment("MT_E2E_BUSINESS_EMAIL", businessEmail)
        .WithEnvironment("MT_E2E_PICKUP_DIR", pickupDirectory)
        .WithEnvironment("MT_E2E_READY_FILE", readyFile)
        .WithHttpsEndpoint(port: 7280, env: "MT_E2E_API_PORT", isProxied: false)
        .WaitFor(postgres);
    front.WithReference(api.GetEndpoint("https")).WaitFor(api);
}
else
{
    var api = builder.AddProject<Projects.ArquitecturaBaseMultitenant_Api>("api")
        .WithEnvironment("Authentication__Issuer", issuer)
        .WithReference(appDb).WithReference(appDbAdmin).WithReference(postgresBootstrap)
        .WaitFor(postgres);
    front.WithReference(api).WaitFor(api);
    // Copiado de ArquitecturaBase: el túnel local expone sólo HTTPS de la API para Meta.
    if (bool.TryParse(builder.Configuration["DevTunnel:Enabled"], out var enabled) && enabled)
    {
        var options = new DevTunnelOptions { Region = DevTunnelRegion.BrazilSouth };
        var id = builder.Configuration["DevTunnel:TunnelId"];
        builder.AddDevTunnel("tunnel", tunnelId: string.IsNullOrWhiteSpace(id) ? null : id, options: options)
            .WithReference(api.GetEndpoint("https"), allowAnonymous: true);
    }
}

builder.Build().Run();
