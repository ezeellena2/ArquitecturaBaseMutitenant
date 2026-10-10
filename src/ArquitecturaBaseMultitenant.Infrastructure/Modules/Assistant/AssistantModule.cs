using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using Microsoft.Extensions.Options;
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
        services.AddScoped<IAssistantSettingsRepository, AssistantSettingsRepository>();
        services.AddScoped<IAssistantModelRepository, AssistantModelRepository>();
        services.AddScoped<IAssistantModelReader, AssistantModelReader>();
        services.AddSingleton<IAssistantProviderCatalog, AssistantProviderCatalog>();
        services.AddScoped<IAssistantKeyProtector, AssistantKeyProtector>();
        services.AddScoped<IChatClientFactory, AnthropicChatClientFactory>();
        services.AddSingleton<IValidateOptions<AssistantOptions>, AssistantOptionsValidator>();
        services.AddSingleton<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Help.IHelpArticleReader,
            ArquitecturaBaseMultitenant.Infrastructure.Help.RepositoryHelpArticleReader>();
        services.AddScoped<IModuleDataSeedContributor, AssistantReferenceDataSeed>();
        return services;
    }
}
