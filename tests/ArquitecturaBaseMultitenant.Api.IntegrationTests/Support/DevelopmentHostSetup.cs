using Microsoft.AspNetCore.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

internal static class DevelopmentHostSetup
{
    public static IWebHostBuilder UseDevelopmentTestOwner(this IWebHostBuilder builder) =>
        builder.UseEnvironment("Development")
            .UseSetting("Seed:PlatformOwner:Email", "operator@example.test");
}
