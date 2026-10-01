using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba que salir dirija a la página pública inicial. Evita continuar en el destino del acceso que se
/// acaba de cerrar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LogoutTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Logout_redirects_to_the_public_home_instead_of_the_previous_access()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(
            "/connect/logout?client_id=web&post_logout_redirect_uri=https%3A%2F%2Flocalhost%3A5174%2F", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://localhost:5174/", response.Headers.Location?.ToString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
    }
}
