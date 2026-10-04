using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Registra los casos de uso de Plataforma.</summary>
internal static class PlatformRegistration
{
    internal static IServiceCollection AddPlatform(this IServiceCollection services)
    {
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<PlatformSettingsAuditWriter>();
        services.AddScoped<IPlatformFeatureService, PlatformFeatureService>();
        services.AddScoped<PlatformFeatureListBuilder>();
        services.AddScoped<PlatformFeatureWriter>();
        services.AddScoped<ITenantAdministrationService, TenantAdministrationService>();
        services.AddScoped<PlatformTenantStateWriter>();
        services.AddScoped<PlatformAuditWriter>();
        services.AddScoped<PlatformTenantNotifier>();
        services.AddScoped<IPlatformScopeAccessService, PlatformScopeAccessService>();
        services.AddScoped<IPlatformPublicSiteService, PlatformPublicSiteService>();
        services.AddScoped<PlatformPublicSiteWriter>();
        services.AddScoped<IPlatformDomainService, PlatformDomainService>();
        services.AddScoped<IPlatformAccountService, PlatformAccountService>();
        services.AddScoped<IPlatformAccountStateService, PlatformAccountStateService>();
        services.AddScoped<IPlatformAccountDeletionService, PlatformAccountDeletionService>();
        services.AddScoped<IPlatformRecoveryService, PlatformRecoveryService>();
        services.AddScoped<IPlatformRecoveryDecisionService, PlatformRecoveryDecisionService>();
        services.AddScoped<PlatformRecoveryNotifier>();
        services.AddScoped<IPlatformOperatorInvitationService, PlatformOperatorInvitationService>();
        services.AddScoped<IPlatformOperatorService, PlatformOperatorService>();
        services.AddScoped<PlatformOperatorProvisioner>();
        services.AddScoped<PlatformOperatorInvitationNotifier>();
        services.AddScoped<IPlatformAuditService, PlatformAuditService>();
        services.AddScoped<IPlatformLegalService, PlatformLegalService>();
        services.AddScoped<PlatformOperatorGuard>();
        services.AddScoped<PlatformActionGuard>();
        services.AddScoped<PlatformTenantSlugResolver>();
        services.AddScoped<PlatformTenantCreator>();
        services.AddScoped<PlatformTenantActivator>();
        return services;
    }
}
