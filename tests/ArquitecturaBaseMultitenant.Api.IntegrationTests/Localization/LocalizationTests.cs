using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Localization;

[Collection(ApiTestGroup.Name)]
public sealed class LocalizationTests(ApiFactory factory)
{
    [Theory]
    [InlineData(null, "Datos inválidos", "La solicitud tiene un formato inválido.", "es-AR")]
    [InlineData("es-AR", "Datos inválidos", "La solicitud tiene un formato inválido.", "es-AR")]
    [InlineData("en", "Invalid data", "The request has an invalid format.", "en-US")]
    [InlineData("en-US", "Invalid data", "The request has an invalid format.", "en-US")]
    [InlineData("fr-FR,en-US;q=0.8", "Invalid data", "The request has an invalid format.", "en-US")]
    public async Task Problem_follows_accept_language_and_declares_response_language(
        string? language, string title, string detail, string contentLanguage)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/result/Validation");
        if (language is not null)
        {
            request.Headers.AcceptLanguage.ParseAdd(language);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(title, document.RootElement.GetProperty("title").GetString());
        Assert.Equal(detail, document.RootElement.GetProperty("detail").GetString());
        Assert.Equal("Request.Invalid", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(contentLanguage, Assert.Single(response.Content.Headers.ContentLanguage));
    }

    [Theory]
    [InlineData("es-AR", "No encontrado", "No encontramos lo que buscás.")]
    [InlineData("en-US", "Not found", "We couldn't find what you're looking for.")]
    public async Task Framework_problem_uses_request_language(string language, string title, string detail)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/does-not-exist");
        request.Headers.AcceptLanguage.ParseAdd(language);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(title, document.RootElement.GetProperty("title").GetString());
        Assert.Equal(detail, document.RootElement.GetProperty("detail").GetString());
        Assert.Equal(language, Assert.Single(response.Content.Headers.ContentLanguage));
    }
}
