var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 5434)
    .WithDataVolume("arquitecturabase-multitenant-pgdata")
    .WithLifetime(ContainerLifetime.Persistent);

var appDb = postgres.AddDatabase("appdb");

var api = builder.AddProject<Projects.ArquitecturaBaseMultitenant_Api>("api")
    .WithReference(appDb)
    .WaitFor(appDb);

builder.AddViteApp("front", "../../../ArquitecturaBaseMutitenantFront")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpsEndpoint(port: 5174, env: "PORT", isProxied: false)
    .WithHttpsDeveloperCertificate();

builder.Build().Run();
