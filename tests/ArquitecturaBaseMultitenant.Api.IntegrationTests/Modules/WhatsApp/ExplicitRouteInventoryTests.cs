namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

public sealed partial class ExplicitRouteInventoryTests
{
    static partial void AddWhatsAppRoutes(List<string> routes) => routes.AddRange(
    [
        "GET /webhooks/whatsapp",
        "POST /webhooks/whatsapp",
    ]);
}
