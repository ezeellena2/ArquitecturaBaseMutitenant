using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class ApplicationServicesTests
{
    static partial void AddWhatsAppServices(IServiceCollection services) =>
        ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.WhatsAppModule.AddWhatsAppModule(services);
}
