using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba que los documentos legales se puedan consultar públicamente y que las rutas del perfil
/// requieran sesión. Protege la separación entre lectura pública y cuenta.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class MeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("terms", "es-AR", "DOCUMENTO DE DEMOSTRACIÓN.")]
    [InlineData("privacy", "en-US", "DEMONSTRATION DOCUMENT.")]
    public async Task Current_legal_document_is_public_in_the_requested_culture(
        string kind, string culture, string expectedPrefix)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/legal/{kind}");
        request.Headers.AcceptLanguage.ParseAdd(culture);

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<LegalBody>(Ct);
        Assert.NotNull(document);
        Assert.Equal(1, document.Version);
        Assert.Equal(culture, document.Culture);
        Assert.StartsWith(expectedPrefix, document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_routes_require_an_authenticated_access()
    {
        using var client = factory.CreateClient();

        using var get = await client.GetAsync("/api/me", Ct);
        using var put = await client.PutAsJsonAsync("/api/me",
            new { displayName = "Ana", culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    private sealed record LegalBody(int Version, string Culture, string Text);
}
