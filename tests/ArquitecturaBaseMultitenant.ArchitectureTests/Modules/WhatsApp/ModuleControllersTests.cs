namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class ModuleControllersTests
{
    static partial void AddWhatsAppControllers(Microsoft.Extensions.DependencyInjection.IServiceCollection registrations)
    {
        ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.WhatsAppModule.AddWhatsAppModule(registrations);
        ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.WhatsAppInfrastructureModule.AddWhatsAppModule(registrations, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.WhatsAppApiModule.AddWhatsAppModule(registrations);
    }
}
