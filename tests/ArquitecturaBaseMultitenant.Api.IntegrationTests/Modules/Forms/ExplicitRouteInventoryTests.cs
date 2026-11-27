namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

public sealed partial class ExplicitRouteInventoryTests
{
    static partial void AddFormsRoutes(List<string> routes) => routes.AddRange(
    [
        "GET /api/organization/forms/catalogs",
        "POST /api/organization/forms/catalogs",
        "GET /api/organization/forms/catalogs/filter-counts",
        "GET /api/organization/forms/catalogs/{id:guid}",
        "PUT /api/organization/forms/catalogs/{id:guid}",
        "GET /api/organization/forms/catalogs/{id:guid}/items",
        "POST /api/organization/forms/catalogs/{id:guid}/imports/previews",
        "POST /api/organization/forms/catalogs/{id:guid}/imports",
        "GET /api/organization/forms/catalogs/{id:guid}/export",
        "GET /api/organization/forms/catalogs/template",
        "GET /api/organization/forms/catalogs/sources/members",
        "GET /api/organization/forms/catalogs/sources/members/roles",
        "GET /api/organization/forms/catalogs/sources/members/companies",
        "GET /api/organization/forms/catalogs/sources/contacts/tags",
        "GET /api/organization/forms/catalogs/sample-members",
        "GET /api/organization/forms/catalogs/sample-contacts",
        "POST /api/organization/forms/catalogs/previews",
    ]);
}
