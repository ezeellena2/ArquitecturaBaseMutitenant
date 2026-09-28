using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

/// <summary>Inventario explícito de combinaciones verbo y ruta públicas de la Api.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class ExplicitRouteInventoryTests(ApiFactory factory)
{
    private static readonly string[] ExpectedRoutes =
    [
        "GET /api/reference-data",
        "GET /api/reference-data/currencies",
        "GET /api/reference-data/countries",
        "GET /api/reference-data/time-zones",
        "GET /api/reference-data/cultures",
        "GET /api/reference-data/tax-id-types",
    ];

    [Fact]
    public void Every_explicit_api_route_is_listed_once()
    {
        var actual = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                var path = "/" + endpoint.RoutePattern.RawText?.Trim('/');
                var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
                return methods.Select(method => $"{method.ToUpperInvariant()} {path.ToLowerInvariant()}");
            })
            .Where(route => route.Contains(" /api/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedRoutes.Order(StringComparer.Ordinal), actual);
    }
}
