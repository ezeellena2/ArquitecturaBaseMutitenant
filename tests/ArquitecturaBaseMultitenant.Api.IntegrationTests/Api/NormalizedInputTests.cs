using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Api;

/// <summary>
/// Comprueba la limpieza de texto en el límite HTTP y el rechazo de valores no textuales. Verifica la
/// entrada sin depender de persistencia.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class NormalizedInputTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_returns_cleaned_body_without_persistence()
    {
        var raw = "  signed\u200B token  ";
        var json = JsonSerializer.Serialize(new
        {
            name = "  Jose\u0301\u200B  ",
            description = "  Grupo  La Cosecha\nLínea 2 👩\u200D💻 می\u200Cخواهم  ",
            empty = " \u200B ",
            raw,
        });
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/test/normalized-input", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var value = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("José", value.GetProperty("name").GetString());
        Assert.Equal("Grupo  La Cosecha\nLínea 2 👩\u200D💻 می\u200Cخواهم", value.GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, value.GetProperty("empty").ValueKind);
        Assert.Equal(raw, value.GetProperty("raw").GetString());
    }

    [Theory]
    [InlineData("""{"name":42}""")]
    [InlineData("""{"raw":42}""")]
    public async Task Non_text_values_are_rejected_as_a_400_problem(string json)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/test/normalized-input", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Request.Invalid", document.RootElement.GetProperty("code").GetString());
    }
}
