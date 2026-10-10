using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting;

public sealed partial class ServiceProviderValidationTests
{
    [Fact]
    public void Installed_assistant_resolves_its_services_and_persistence_ports()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.NotNull(services.GetRequiredService<IAssistantConversationService>());
        Assert.NotNull(services.GetRequiredService<IAssistantSettingsService>());
        Assert.NotNull(services.GetRequiredService<IAssistantConversationReader>());
        Assert.NotNull(services.GetRequiredService<IAssistantSettingsReader>());
        Assert.NotNull(services.GetRequiredService<IAssistantConversationRepository>());
        Assert.NotNull(services.GetRequiredService<IPlatformAssistantConversationRepository>());
    }

}
