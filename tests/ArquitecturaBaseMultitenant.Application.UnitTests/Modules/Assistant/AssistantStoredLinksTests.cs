using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

/// <summary>
/// Los enlaces de un mensaje se guardan en <c>LinksJson</c> con <see cref="JsonSerializer"/> sin opciones, así que el destino
/// queda como número. Un valor nuevo de <see cref="AssistantRouteKey"/> va al final: un enlace guardado por la versión anterior
/// se sigue leyendo con el mismo destino (migración, «Convivencia de versiones»).
/// </summary>
public sealed class AssistantStoredLinksTests
{
    [Theory]
    [InlineData(0, AssistantRouteKey.BusinessUsers)]
    [InlineData(1, AssistantRouteKey.BusinessRoles)]
    [InlineData(2, AssistantRouteKey.BusinessCompanies)]
    [InlineData(3, AssistantRouteKey.BusinessAudit)]
    [InlineData(4, AssistantRouteKey.BusinessHome)]
    [InlineData(5, AssistantRouteKey.PlatformOrganizations)]
    [InlineData(6, AssistantRouteKey.PlatformAccounts)]
    [InlineData(7, AssistantRouteKey.PlatformRecoveries)]
    [InlineData(8, AssistantRouteKey.PlatformAudit)]
    [InlineData(9, AssistantRouteKey.PlatformAssistantUsage)]
    [InlineData(10, AssistantRouteKey.PersonalAccount)]
    [InlineData(11, AssistantRouteKey.PersonalOrganizations)]
    [InlineData(12, AssistantRouteKey.BusinessFormCatalogs)]
    public void A_stored_link_keeps_its_route(int stored, AssistantRouteKey route)
    {
        var json = $$"""[{"RouteKey":{{stored}},"Parameters":{},"Query":{},"LabelKey":"label"}]""";

        var link = Assert.Single(JsonSerializer.Deserialize<AssistantLinkResponse[]>(json)!);

        Assert.Equal(route, link.RouteKey);
        Assert.Contains($"\"RouteKey\":{stored}", JsonSerializer.Serialize(new[] { link }), StringComparison.Ordinal);
    }
}
