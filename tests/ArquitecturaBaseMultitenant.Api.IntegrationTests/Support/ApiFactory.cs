using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }
}
