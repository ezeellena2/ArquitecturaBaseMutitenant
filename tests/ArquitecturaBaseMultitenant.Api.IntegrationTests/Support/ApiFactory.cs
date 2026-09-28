using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureTestServices(services =>
            services.AddControllers().ConfigureApplicationPartManager(parts =>
                parts.ApplicationParts.Add(new TestControllerApplicationPart())));
    }
}
