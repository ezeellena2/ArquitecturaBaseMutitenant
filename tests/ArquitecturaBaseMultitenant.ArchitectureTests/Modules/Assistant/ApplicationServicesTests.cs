using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class ApplicationServicesTests
{
    static partial void AddAssistantServices(IServiceCollection services) =>
        ArquitecturaBaseMultitenant.Application.Modules.Assistant.AssistantModule.AddAssistantModule(services);
}
