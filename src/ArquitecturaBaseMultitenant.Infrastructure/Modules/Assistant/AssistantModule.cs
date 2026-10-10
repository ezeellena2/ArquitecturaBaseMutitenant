using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
public static class AssistantModule
{
    public static IServiceCollection AddAssistantModule(this IServiceCollection services)
    {
        services.AddSingleton<IPersistenceModelContributor, AssistantModelContributor>();
        services.AddScoped<IAssistantSettingsReader, AssistantSettingsReader>();
        services.AddScoped<IAssistantConversationReader, AssistantConversationReader>();
        services.AddScoped<IAssistantConversationRepository, AssistantConversationRepository>();
        services.AddScoped<IPlatformAssistantConversationRepository, PlatformAssistantConversationRepository>();
        return services;
    }
}
