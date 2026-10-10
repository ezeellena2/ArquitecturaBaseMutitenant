using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant;

public static class AssistantModule
{
    public static IServiceCollection AddAssistantModule(this IServiceCollection services)
    {
        services.AddScoped<IAssistantConversationService, AssistantConversationService>();
        services.AddScoped<IAssistantSettingsService, AssistantSettingsService>();
        services.AddScoped<IAssistantUsageService, AssistantUsageService>();
        services.AddScoped<IAssistantModelService, AssistantModelService>();
        services.AddScoped<IAssistantPolicyService, AssistantPolicyService>();
        services.AddSingleton<IFeatureDefinitionContributor, AssistantFeatureDefinitionContributor>();
        services.AddScoped<IFeatureAvailabilityPolicy, AssistantAvailabilityPolicy>();
        services.AddScoped<IModuleCapabilityContributor, AssistantCapabilityContributor>();
        services.AddSingleton<IErrorTextSource, AssistantErrorTextSource>();
        return services;
    }
}
