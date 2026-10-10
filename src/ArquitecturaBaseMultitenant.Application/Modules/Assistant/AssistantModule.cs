using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

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
        services.AddScoped<AssistantSettingsBuilder>();
        services.AddScoped<AssistantSettingsSelectionPolicy>();
        services.AddScoped<AssistantSettingsWriter>();
        services.AddScoped<AssistantSettingsAuditWriter>();
        services.AddScoped<AssistantModelWriter>();
        services.AddScoped<IAssistantConfigurationPolicy, AssistantConfigurationPolicy>();
        services.AddOptions<AssistantOptions>().BindConfiguration(AssistantOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IFeatureDefinitionContributor, AssistantFeatureDefinitionContributor>();
        services.AddScoped<IFeatureAvailabilityPolicy, AssistantAvailabilityPolicy>();
        services.AddScoped<IModuleCapabilityContributor, AssistantCapabilityContributor>();
        services.AddSingleton<IErrorTextSource, AssistantErrorTextSource>();
        return services;
    }
}
