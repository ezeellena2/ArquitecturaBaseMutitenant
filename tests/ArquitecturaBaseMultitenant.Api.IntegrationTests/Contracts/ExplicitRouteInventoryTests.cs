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
        "GET /api/auth/external/google/antiforgery",
        "POST /api/auth/external/google",
        "GET /api/auth/external/callback",
        "POST /api/auth/login-code",
        "POST /api/auth/login-code/verify",
        "POST /api/account-recovery/code",
        "POST /api/account-recovery/verify",
        "POST /api/account-recovery",
        "GET /api/contact",
        "POST /api/auth/deletion/pending",
        "POST /api/auth/deletion/cancel",
        "GET /api/auth/methods",
        "POST /api/auth/signup",
        "POST /api/auth/signup/verify",
        "POST /api/auth/business-signup/code",
        "POST /api/auth/business-signup/verify",
        "POST /api/auth/business-signup",
        "GET /api/public-site/slug-availability",
        "POST /api/invitations/preview",
        "POST /api/invitations/accept",
        "GET /api/home",
        "GET /api/users/paged",
        "GET /api/users/filter-counts",
        "GET /api/users/{memberid:guid}",
        "GET /api/organization/settings",
        "PUT /api/organization/settings",
        "GET /api/organization/audit",
        "GET /api/organization/audit/actors",
        "GET /api/organization/public-page",
        "PUT /api/organization/public-page",
        "POST /api/organization/public-page/publish",
        "POST /api/organization/public-page/unpublish",
        "GET /api/companies/paged",
        "GET /api/companies/filter-counts",
        "GET /api/companies/{companyid:guid}",
        "POST /api/companies",
        "PUT /api/companies/{companyid:guid}",
        "PUT /api/companies/{companyid:guid}/status",
        "GET /api/companies/{companyid:guid}/members/paged",
        "GET /api/companies/{companyid:guid}/members/filter-counts",
        "POST /api/companies/{companyid:guid}/members",
        "PUT /api/companies/{companyid:guid}/members/{memberid:guid}/roles",
        "PUT /api/companies/{companyid:guid}/members/{memberid:guid}/status",
        "GET /api/companies/{companyid:guid}/roles/paged",
        "GET /api/companies/{companyid:guid}/candidates/paged",
        "POST /api/companies/{companyid:guid}/invitations/{invitationid:guid}/resend",
        "DELETE /api/companies/{companyid:guid}/invitations/{invitationid:guid}",
        "GET /api/organization/settings/domains",
        "POST /api/organization/settings/domains",
        "POST /api/organization/settings/domains/{domainid:guid}/verify",
        "DELETE /api/organization/settings/domains/{domainid:guid}",
        "POST /api/users/invitations",
        "POST /api/users/invitations/{invitationid:guid}/resend",
        "DELETE /api/users/invitations/{invitationid:guid}",
        "PUT /api/users/{memberid:guid}",
        "PUT /api/users/{memberid:guid}/status",
        "DELETE /api/users/{memberid:guid}",
        "GET /api/permissions",
        "GET /api/roles/paged",
        "GET /api/roles/{id:guid}",
        "POST /api/roles",
        "PUT /api/roles/{id:guid}",
        "DELETE /api/roles/{id:guid}",
        "GET /api/legal/terms",
        "GET /api/legal/privacy",
        "POST /api/legal/accept",
        "GET /api/me",
        "PUT /api/me",
        "GET /api/platform/settings",
        "PUT /api/platform/settings",
        "GET /api/platform/tenants/paged",
        "GET /api/platform/tenants/filter-counts",
        "GET /api/platform/tenants/{id:guid}",
        "GET /api/platform/tenants/{id:guid}/history",
        "POST /api/platform/tenants",
        "POST /api/platform/tenants/{id:guid}/approve",
        "POST /api/platform/tenants/{id:guid}/retry-provisioning",
        "POST /api/platform/tenants/{id:guid}/reject",
        "POST /api/platform/tenants/{id:guid}/suspend",
        "POST /api/platform/tenants/{id:guid}/reactivate",
        "POST /api/platform/tenants/{id:guid}/close",
        "GET /api/platform/tenants/{id:guid}/features",
        "PUT /api/platform/tenants/{id:guid}/features/{featurekey}",
        "GET /api/platform/tenants/{id:guid}/public-site",
        "POST /api/platform/tenants/{id:guid}/public-site/unpublish",
        "POST /api/platform/tenants/{id:guid}/public-site/allow-publish",
        "GET /api/platform/tenants/{id:guid}/domains",
        "POST /api/platform/tenants/{id:guid}/domains/{domain}/verify",
        "DELETE /api/platform/tenants/{id:guid}/domains/{domain}",
        "GET /api/platform/accounts/paged",
        "GET /api/platform/accounts/filter-counts",
        "GET /api/platform/accounts/{id:guid}",
        "POST /api/platform/accounts/{id:guid}/suspend",
        "POST /api/platform/accounts/{id:guid}/reactivate",
        "POST /api/platform/accounts/{id:guid}/revoke-sessions",
        "POST /api/platform/accounts/{id:guid}/deletion",
        "POST /api/platform/operators/invitations",
        "POST /api/platform/operators/{id:guid}/invitation/resend",
        "DELETE /api/platform/operators/{id:guid}/invitation",
        "GET /api/platform/audit",
        "GET /api/platform/legal-documents",
        "GET /api/platform/legal-documents/{id:guid}",
        "POST /api/platform/legal-documents",
        "GET /api/platform/audit/{id:guid}",
        "PUT /api/platform/accounts/{id:guid}/operator",
        "DELETE /api/platform/accounts/{id:guid}/operator",
        "GET /api/platform/recoveries/paged",
        "GET /api/platform/recoveries/filter-counts",
        "POST /api/platform/recoveries/{id:guid}/approve",
        "POST /api/platform/recoveries/{id:guid}/reject",
        "POST /api/me/external/google",
        "GET /api/me/login-methods",
        "POST /api/me/login-methods",
        "POST /api/me/login-methods/{methodid:guid}/code",
        "POST /api/me/login-methods/{methodid:guid}/verify",
        "PUT /api/me/login-methods/{methodid:guid}/primary",
        "DELETE /api/me/login-methods/{methodid:guid}",
        "POST /api/me/reauth",
        "POST /api/me/reauth/verify",
        "POST /api/me/deletion",
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
