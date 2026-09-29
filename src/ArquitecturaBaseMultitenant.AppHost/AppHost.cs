var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var ownerPassword = builder.AddParameter("mt-owner-password", secret: true);
var runtimePassword = builder.AddParameter("mt-app-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 5434)
    .WithDataVolume("arquitecturabase-multitenant-pgdata")
    .WithLifetime(ContainerLifetime.Persistent);

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

builder.AddViteApp("front", "../../../ArquitecturaBaseMutitenantFront")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpsEndpoint(port: 5174, env: "PORT", isProxied: false)
    .WithHttpsDeveloperCertificate();

builder.Build().Run();
