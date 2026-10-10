namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class ModuleControllersTests
{
    static partial void AddAssistantControllers(Microsoft.Extensions.DependencyInjection.IServiceCollection registrations)
    {
        ArquitecturaBaseMultitenant.Application.Modules.Assistant.AssistantModule.AddAssistantModule(registrations);
        ArquitecturaBaseMultitenant.Api.Modules.Assistant.AssistantApiModule.AddAssistantModule(registrations);
    }
}
