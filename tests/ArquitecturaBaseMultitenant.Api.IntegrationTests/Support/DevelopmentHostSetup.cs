using Microsoft.AspNetCore.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Configura un operador sintético para los tests que necesitan iniciar el host en Development.
/// Evita depender del user-secret personal de quien ejecuta la suite.
/// </summary>
internal static class DevelopmentHostSetup
{
    public static IWebHostBuilder UseDevelopmentTestOwner(this IWebHostBuilder builder) =>
        builder.UseEnvironment("Development")
            .UseSetting("Seed:PlatformOwner:Email", "operator@example.test");
}
