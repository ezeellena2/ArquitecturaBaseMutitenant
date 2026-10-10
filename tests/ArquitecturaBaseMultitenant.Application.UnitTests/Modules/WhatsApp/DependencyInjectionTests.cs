using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.UnitTests;

public sealed partial class DependencyInjectionTests
{
    static partial void AddWhatsAppServices(IServiceCollection services) =>
        ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.WhatsAppModule.AddWhatsAppModule(services);
}
