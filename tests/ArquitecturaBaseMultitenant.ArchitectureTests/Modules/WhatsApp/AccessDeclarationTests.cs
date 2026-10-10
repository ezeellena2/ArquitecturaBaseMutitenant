namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class AccessDeclarationTests
{
    static partial void AddWhatsAppAnonymousController(HashSet<Type> controllers) =>
        controllers.Add(typeof(ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers.WhatsAppWebhookController));
}
