using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Json;

/// <summary>
/// textos-libres.md, nivel 1: las partes que lleva adentro un pedido (<c>*HttpInput</c>, como una opción o los filtros de un
/// catálogo) limpian sus textos igual que el pedido: sin espacios a los lados, sin invisibles peligrosos y vacío como null.
/// </summary>
public sealed class NestedInputJsonTests
{
    [Fact]
    public void The_parts_of_a_request_clean_their_texts_like_the_request()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ConfigureJson(options);
        const string json = """
            {"name":" Colores ","items":[{"localId":"n1","code":" ROJ​ ","text":"  Rojo­  ","detail":"   "}],"filters":{"detail":" role "}}
            """;

        var request = JsonSerializer.Deserialize<CreateFormCatalogHttpRequest>(json, options)!;

        Assert.Equal("Colores", request.Name);
        var item = Assert.Single(request.Items!);
        Assert.Equal(("ROJ", "Rojo", (string?)null), (item.Code, item.Text, item.Detail));
        Assert.Equal("role", request.Filters!.Detail);
    }
}
