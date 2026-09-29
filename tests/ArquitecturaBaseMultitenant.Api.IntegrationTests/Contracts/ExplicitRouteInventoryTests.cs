using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
        "GET /api/auth/external/google",
        "GET /api/auth/external/callback",
        "POST /api/auth/login-code",
        "POST /api/auth/login-code/verify",
        "GET /api/auth/methods",
        "POST /api/auth/signup",
        "POST /api/auth/signup/verify",
        "GET /api/legal/terms",
        "GET /api/legal/privacy",
        "GET /api/me",
        "PUT /api/me",
        "GET /connect/authorize",
        "POST /connect/authorize",
        "POST /connect/token",
        "GET /connect/logout",
        "POST /connect/logout",
        "GET /connect/userinfo",
        "POST /connect/userinfo",
    ];

    [Fact]
    public void Every_explicit_api_route_is_listed_once()
    {
        var productionAssembly = typeof(Program).Assembly;
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not { } action
                || action.ControllerTypeInfo.Assembly == productionAssembly);
        var actual = InventoriedRoutes(endpoints);

        Assert.DoesNotContain(actual, route => route.StartsWith("<ANY> ", StringComparison.Ordinal));
        Assert.Empty(MissingBackendPrefixes(actual, BackendPrefixes()));
        Assert.Equal(ExpectedRoutes.Order(StringComparer.Ordinal), actual);
    }

    [Fact]
    public void Inventory_includes_routes_without_a_verb_and_outside_api_but_omits_health()
    {
        var routes = new[]
        {
            Route("api/without-verb"),
            Route("account/start", "POST"),
            Route("health", "GET"),
        };

        Assert.Equal(new[] { "<ANY> /api/without-verb", "POST /account/start" }, InventoriedRoutes(routes));
    }

    [Fact]
    public void Every_inventoried_route_must_have_a_backend_prefix()
    {
        Assert.Contains("GET /unreserved/path",
            MissingBackendPrefixes(["GET /unreserved/path"], ["/api", "/account"]));
    }

    private static RouteEndpoint Route(string path, string? method = null)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse(path), 0);
        if (method is not null)
        {
            builder.Metadata.Add(new HttpMethodMetadata([method]));
        }

        return (RouteEndpoint)builder.Build();
    }

    private static string[] InventoriedRoutes(IEnumerable<RouteEndpoint> routes) =>
        routes.SelectMany(endpoint =>
            {
                var path = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).Trim('/');
                if (path is "/health" or "/alive")
                {
                    return [];
                }

                IReadOnlyList<string> methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                    is { Count: > 0 } declared ? declared : ["<ANY>"];
                return methods.Select(method => $"{method.ToUpperInvariant()} {path.ToLowerInvariant()}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] MissingBackendPrefixes(IEnumerable<string> routes, IEnumerable<string> prefixes)
    {
        var allowed = prefixes.ToArray();
        return routes.Where(route =>
            {
                var path = route[(route.IndexOf(' ', StringComparison.Ordinal) + 1)..];
                return !allowed.Any(prefix => path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
            })
            .ToArray();
    }

    private static string[] BackendPrefixes()
    {
        var host = typeof(Program).Assembly.GetType("ArquitecturaBaseMultitenant.Api.Hosting.SpaExtensions", throwOnError: true)!;
        var field = host.GetField("BackendPrefixes", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BackendPrefixes field was not found.");
        return field.GetValue(null) as string[]
            ?? throw new InvalidOperationException("BackendPrefixes must be a string array.");
    }
}
