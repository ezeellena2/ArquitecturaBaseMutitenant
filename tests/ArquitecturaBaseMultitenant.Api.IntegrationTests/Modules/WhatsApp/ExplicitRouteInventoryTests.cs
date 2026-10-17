namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

public sealed partial class ExplicitRouteInventoryTests
{
    static partial void AddWhatsAppRoutes(List<string> routes)
    {
        routes.AddRange(
        [
            "GET /webhooks/whatsapp",
            "POST /webhooks/whatsapp",
            "GET /api/organization/whatsapp/numbers",
            "POST /api/organization/whatsapp/numbers",
            "GET /api/organization/whatsapp/numbers/{id:guid}",
            "GET /api/organization/whatsapp/numbers/company-options",
            "PUT /api/organization/whatsapp/numbers/{id:guid}",
            "DELETE /api/organization/whatsapp/numbers/{id:guid}",
            "POST /api/organization/whatsapp/numbers/{id:guid}/check",
            "GET /api/organization/whatsapp/numbers/{id:guid}/bot",
            "PUT /api/organization/whatsapp/numbers/{id:guid}/bot",
            "GET /api/organization/whatsapp/contacts",
            "GET /api/organization/whatsapp/contacts/{id:guid}",
            "PUT /api/organization/whatsapp/contacts/{id:guid}",
            "GET /api/organization/whatsapp/contacts/{id:guid}/conversations",
            "GET /api/organization/whatsapp/contacts/{id:guid}/broadcasts",
            "GET /api/organization/whatsapp/contacts/tags",
            "GET /api/organization/whatsapp/contacts/import-sample",
            "POST /api/organization/whatsapp/contacts/import/preview",
            "POST /api/organization/whatsapp/contacts/import",
            "GET /api/organization/whatsapp/templates",
            "POST /api/organization/whatsapp/templates",
            "GET /api/organization/whatsapp/templates/{id:guid}",
            "DELETE /api/organization/whatsapp/templates/{id:guid}",
            "GET /api/organization/whatsapp/broadcasts",
            "POST /api/organization/whatsapp/broadcasts",
            "GET /api/organization/whatsapp/broadcasts/estimate",
            "GET /api/organization/whatsapp/broadcasts/template-options",
            "GET /api/organization/whatsapp/broadcasts/tags",
            "GET /api/organization/whatsapp/broadcasts/{id:guid}",
            "POST /api/organization/whatsapp/broadcasts/{id:guid}/cancel",
            "POST /api/organization/whatsapp/phone-imports/preview",
            "POST /api/organization/whatsapp/phone-imports",
            "GET /api/platform/whatsapp/number",
            "POST /api/platform/whatsapp/number",
            "PUT /api/platform/whatsapp/number",
            "POST /api/platform/whatsapp/number/check",
            "GET /api/platform/whatsapp/bot",
            "PUT /api/platform/whatsapp/bot",
            "GET /api/platform/whatsapp/templates",
            "POST /api/platform/whatsapp/templates",
            "GET /api/platform/whatsapp/templates/{id:guid}",
            "DELETE /api/platform/whatsapp/templates/{id:guid}",
            "GET /api/me/whatsapp/conversations",
            "GET /api/me/whatsapp/conversations/{id:guid}",
            "DELETE /api/me/whatsapp/conversations/{id:guid}/link",
            "GET /api/organization/whatsapp/untaken",
        ]);
        foreach (var prefix in new[] { "/api/organization/whatsapp/teams", "/api/platform/whatsapp/teams" })
            routes.AddRange(
            [
                $"GET {prefix}",
                $"POST {prefix}",
                $"GET {prefix}/member-options",
                $"GET {prefix}/{{id:guid}}",
                $"PUT {prefix}/{{id:guid}}",
                $"DELETE {prefix}/{{id:guid}}",
            ]);
        foreach (var prefix in new[] { "/api/organization/whatsapp/customers", "/api/organization/whatsapp/internal", "/api/platform/whatsapp" })
            routes.AddRange(
            [
                $"GET {prefix}/inbox-teams",
                $"GET {prefix}/conversations",
                $"GET {prefix}/conversations/{{id:guid}}",
                $"POST {prefix}/conversations/{{id:guid}}/take",
                $"POST {prefix}/conversations/{{id:guid}}/messages",
                $"POST {prefix}/conversations/{{id:guid}}/notes",
                $"POST {prefix}/conversations/{{id:guid}}/transfer",
                $"GET {prefix}/conversations/{{id:guid}}/template-options",
                $"POST {prefix}/conversations/{{id:guid}}/templates",
                $"GET {prefix}/conversations/{{id:guid}}/transfer-options/teams",
                $"GET {prefix}/conversations/{{id:guid}}/transfer-options/people",
            ]);
    }
}
