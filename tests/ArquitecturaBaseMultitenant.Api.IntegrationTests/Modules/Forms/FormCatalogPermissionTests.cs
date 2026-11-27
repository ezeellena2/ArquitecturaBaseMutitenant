using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// Los permisos de cada ruta de los catálogos, como los fija la tabla de rutas de la spec (§5 de la E12): un miembro sin
/// permisos recibe 403 en todas, y uno con solo <c>formcatalogs.read</c>, en las que piden <c>formcatalogs.manage</c>. La tabla
/// tiene que nombrar cada ruta del controller, así una ruta nueva entra con su permiso.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormCatalogPermissionTests(ApiFactory factory)
{
    private const string Read = Permissions.Forms.CatalogsRead;
    private const string Manage = Permissions.Forms.CatalogsManage;

    private static readonly (string Method, string Route, string Permission)[] Routes =
    [
        ("GET", "", Read),
        ("GET", "/filter-counts", Read),
        ("POST", "", Manage),
        ("GET", "/{id:guid}", Read),
        ("PUT", "/{id:guid}", Manage),
        ("GET", "/{id:guid}/items", Read),
        ("POST", "/{id:guid}/imports/previews", Manage),
        ("POST", "/{id:guid}/imports", Manage),
        ("GET", "/{id:guid}/export", Read),
        ("GET", "/template", Read),
        ("GET", "/sources/members", Manage),
        ("GET", "/sources/members/roles", Manage),
        ("GET", "/sources/members/companies", Manage),
        ("GET", "/sources/contacts/tags", Manage),
        ("GET", "/sample-members", Read),
        ("GET", "/sample-contacts", Read),
        ("POST", "/previews", Read),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_route_answers_403_without_its_permission()
    {
        var actual = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(endpoint => (Path: "/" + (endpoint.RoutePattern.RawText ?? string.Empty).Trim('/'), Endpoint: endpoint))
            .Where(route => route.Path.StartsWith(FormsTestData.Catalogs, StringComparison.Ordinal))
            .SelectMany(route => route.Endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods
                .Select(method => $"{method} {route.Path}"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(Routes.Select(route => $"{route.Method} {FormsTestData.Catalogs}{route.Route}").Order(StringComparer.Ordinal), actual);

        var (_, tenantId, _) = await FormsTestData.BusinessAsync(factory, "Permisos de catálogos");
        var catalog = await FormsTestData.CreateManualAsync(factory, tenantId, "Permisos", ("A", "Uno"));
        using var without = await FormsTestData.MemberAsync(factory, tenantId, permission: null);
        using var reader = await FormsTestData.MemberAsync(factory, tenantId, Read);

        foreach (var (method, route, permission) in Routes)
        {
            var path = FormsTestData.Catalogs + route.Replace("{id:guid}", catalog.Id.ToString("D"), StringComparison.Ordinal);
            using (var denied = await SendAsync(without, method, path))
                Assert.True(denied.StatusCode == HttpStatusCode.Forbidden, $"{method} {path} sin permisos: {(int)denied.StatusCode}");
            using var read = await SendAsync(reader, method, path);
            if (permission == Manage)
                Assert.True(read.StatusCode == HttpStatusCode.Forbidden, $"{method} {path} solo con {Read}: {(int)read.StatusCode}");
            else
                Assert.True(read.StatusCode != HttpStatusCode.Forbidden, $"{method} {path} con {Read}: 403");
        }
    }

    // Cada pedido con un cuerpo vacío del tipo que acepta la ruta (las importaciones, multipart: con otro, el ruteo responde 415
    // antes de mirar el permiso) y su clave de idempotencia: el permiso se mira antes que el cuerpo.
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "GET")
        {
            request.Content = path.Contains("/imports", StringComparison.Ordinal)
                ? new MultipartFormDataContent { { new StringContent("Replace"), "mode" } }
                : JsonContent.Create(new { });
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        }
        return await client.SendAsync(request, Ct);
    }
}
