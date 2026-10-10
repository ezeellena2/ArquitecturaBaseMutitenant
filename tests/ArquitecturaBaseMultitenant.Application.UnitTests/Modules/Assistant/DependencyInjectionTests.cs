using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.UnitTests;

public sealed partial class DependencyInjectionTests
{
    static partial void AddAssistantServices(IServiceCollection services) =>
        ArquitecturaBaseMultitenant.Application.Modules.Assistant.AssistantModule.AddAssistantModule(services);
}
