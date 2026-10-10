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
        services.AddScoped<AssistantSessionPolicy>();
        services.AddScoped<AssistantAccessPolicy>();
        services.AddScoped<AssistantHelpAvailabilityPolicy>();
        services.AddScoped<AssistantInteractionsBuilder>();
        services.AddScoped<AssistantToolResolver>();
        services.AddScoped<AssistantQuickResponseBuilder>();
        services.AddScoped<IAssistantQuickService, AssistantQuickService>();
        services.AddScoped<Tools.AssistantToolCatalog>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.OrganizationUsersTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.FindOrganizationPersonTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.RoleMembersTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.OrganizationCompaniesTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.CompanyMembersTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.OrganizationInvitationsTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.OrganizationAuditTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.OrganizationPendingTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Platform.PlatformOrganizationsTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Platform.FindPlatformOrganizationTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Platform.PlatformAccountsTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Platform.PlatformRecoveriesTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Platform.PlatformSecurityTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Business.OrganizationRolesTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Platform.PlatformAssistantUsageTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Personal.MyAccountTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Personal.MyOrganizationsTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.Personal.MyInteractionsTool>();
        services.AddScoped<Tools.IAssistantTool, Tools.HelpArticleTool>();
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
