namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

public sealed partial class RouteIsolationMatrixTests
{
    private const string CatalogsPending = "Las rutas de los catálogos responden 501 hasta la B8: todavía no hay datos de nadie";

    /// <summary>
    /// Contrato de la E12 (K1): las rutas con id responden 501 hasta la B8, que cambia estos casos por sus pruebas con el catálogo
    /// de otra organización (FormCatalogIsolationTests).
    /// </summary>
    static partial void AddFormsCases(Dictionary<string, RouteCase> cases)
    {
        foreach (var route in new[]
        {
            "GET /api/organization/forms/catalogs/{id:guid}",
            "PUT /api/organization/forms/catalogs/{id:guid}",
            "GET /api/organization/forms/catalogs/{id:guid}/items",
            "POST /api/organization/forms/catalogs/{id:guid}/imports/previews",
            "POST /api/organization/forms/catalogs/{id:guid}/imports",
            "GET /api/organization/forms/catalogs/{id:guid}/export",
        })
            cases[route] = Shared(CatalogsPending);
    }
}
