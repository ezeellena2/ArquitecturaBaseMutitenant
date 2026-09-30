var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var ownerPassword = builder.AddParameter("mt-owner-password", secret: true);
var runtimePassword = builder.AddParameter("mt-app-password", secret: true);
var isolatedE2e = Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") == "1";

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

var api = builder.AddProject<Projects.ArquitecturaBaseMultitenant_Api>("api")
    .WithReference(appDb)
    .WithReference(appDbAdmin)
    .WithReference(postgresBootstrap)
    .WaitFor(postgres);

if (isolatedE2e)
{
    api.WithHttpHealthCheck("/alive");
    var businessEmail = Environment.GetEnvironmentVariable("MT_E2E_BUSINESS_EMAIL")
        ?? throw new InvalidOperationException("MT_E2E_BUSINESS_EMAIL is required for the isolated E2E run.");
    var pickupDirectory = Environment.GetEnvironmentVariable("MT_E2E_PICKUP_DIR")
        ?? throw new InvalidOperationException("MT_E2E_PICKUP_DIR is required for the isolated E2E run.");
    var readyFile = Environment.GetEnvironmentVariable("MT_E2E_READY_FILE")
        ?? throw new InvalidOperationException("MT_E2E_READY_FILE is required for the isolated E2E run.");
    if (!Path.IsPathFullyQualified(pickupDirectory) || !Path.IsPathFullyQualified(readyFile))
    {
        throw new InvalidOperationException("The isolated E2E paths must be absolute.");
    }

    api.WithEnvironment("Email__Delivery", "PickupDirectory")
        .WithEnvironment("Email__PickupDirectory", pickupDirectory)
        .WithEnvironment("Seed__PlatformOwner__Email", "e2e-operator@example.test")
        .WithEnvironment("Seed__PlatformOwner__DisplayName", "Operador E2E")
        .WithEnvironment("Seed__PlatformOwner__Phone", "");

    var setupProject = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "tools",
        "ArquitecturaBaseMultitenant.RealE2ESetup", "ArquitecturaBaseMultitenant.RealE2ESetup.csproj"));
    builder.AddExecutable("e2e-setup", "dotnet", builder.AppHostDirectory,
            "run", "--project", setupProject, "--no-build", "--no-launch-profile")
        .WithReference(appDb)
        .WithEnvironment("MT_E2E_ISOLATED", "1")
        .WithEnvironment("MT_E2E_BUSINESS_EMAIL", businessEmail)
        .WithEnvironment("MT_E2E_READY_FILE", readyFile)
        .WaitFor(api);
}

builder.AddViteApp("front", "../../../ArquitecturaBaseMutitenantFront")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpsEndpoint(port: 5174, env: "PORT", isProxied: false)
    .WithHttpsDeveloperCertificate();

builder.Build().Run();
