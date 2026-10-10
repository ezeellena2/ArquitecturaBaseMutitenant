using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;

/// <summary>Composición privada del núcleo E7, reutilizable al quitar módulos de la copia.</summary>
public static class RecorridoApiHost
{
    public static WebApplicationFactory<Program> Configure(ApiFactory factory, string root, string pickup,
        Action<IWebHostBuilder>? configureModules = null) => factory.WithWebHostBuilder(builder =>
    {
        if (!Path.IsPathFullyQualified(pickup) || !Directory.Exists(pickup))
            throw new InvalidOperationException("Existing absolute pickup directory required.");
        var content = Path.Combine(root, "src/ArquitecturaBaseMultitenant.Api");
        const string issuer = "https://localtest.me:5174/";
        builder.UseEnvironment("Testing").UseContentRoot(content)
            .UseSetting("Authentication:Issuer", issuer)
            .UseSetting("Authentication:Clients:Web:RedirectUris:0", issuer + "auth/callback")
            .UseSetting("Authentication:Clients:Web:RedirectUris:1", issuer + "silent-renew.html")
            .UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:0", issuer)
            .UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:1", issuer + "invitacion")
            .UseSetting("Authentication:Google:ClientId", "e2e-synthetic-client.apps.googleusercontent.com")
            .UseSetting("Authentication:Google:ClientSecret", "e2e-synthetic-google-secret")
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureAppConfiguration((_, configuration) =>
            {
                // No user-secrets, Development JSON ni variables de proveedores: sólo las conexiones explícitas.
                configuration.Sources.Clear();
                configuration.AddJsonFile(Path.Combine(content, "appsettings.json"), optional: false);
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:postgres-bootstrap"] = factory.BootstrapConnectionString,
                    ["ConnectionStrings:appdb-admin"] = factory.AdminConnectionString,
                    ["ConnectionStrings:appdb"] = factory.RuntimeConnectionString,
                    ["Authentication:Issuer"] = issuer,
                    ["Authentication:Clients:Web:RedirectUris:0"] = issuer + "auth/callback",
                    ["Authentication:Clients:Web:RedirectUris:1"] = issuer + "silent-renew.html",
                    ["Authentication:Clients:Web:PostLogoutRedirectUris:0"] = issuer,
                    ["Authentication:Clients:Web:PostLogoutRedirectUris:1"] = issuer + "invitacion",
                    ["Authentication:LoginCode:HashKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                    ["Authentication:LoginCode:ResendCooldownSeconds"] = "0",
                    ["Authentication:LoginCode:MaxRequestsPerWindow"] = "100",
                    ["Authentication:Google:ClientId"] = "e2e-synthetic-client.apps.googleusercontent.com",
                    ["Authentication:Google:ClientSecret"] = "e2e-synthetic-google-secret",
                    ["RateLimiting:LoginCodePermitLimit"] = "1000",
                    ["RateLimiting:LoginVerifyPermitLimit"] = "1000",
                    ["Email:Delivery"] = "PickupDirectory",
                    ["Email:PickupDirectory"] = pickup,
                    ["Seed:PlatformOwner:Email"] = "e2e-operator@example.test",
                    ["Seed:PlatformOwner:FirstName"] = "Operador",
                    ["Seed:PlatformOwner:LastName"] = "E2E",
                    ["Seed:PlatformOwner:Phone"] = "",
                    ["WhatsApp:PhoneNumberId"] = "",
                });
            });
        configureModules?.Invoke(builder);
    });
}
