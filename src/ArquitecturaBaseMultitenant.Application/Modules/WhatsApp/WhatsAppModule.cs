using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp;

/// <summary>Registra casos de uso y adaptadores a los puertos neutrales del núcleo.</summary>
public static class WhatsAppModule
{
    public static IServiceCollection AddWhatsAppModule(this IServiceCollection services)
    {
        services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        services.AddScoped<IWhatsAppMessageRetentionService, WhatsAppMessageRetentionService>();
        services.AddScoped<WhatsAppContactRemover>();
        services.AddScoped<Interfaces.Integrations.IWhatsAppInboundConsumer, WhatsAppBotWriter>();
        services.AddScoped<Interfaces.Integrations.IWhatsAppInboundConsumer, WhatsAppOrganizationInboundWriter>();
        services.AddScoped<Interfaces.Integrations.IWhatsAppInboundConsumer, PlatformWhatsAppInboundWriter>();
        services.AddScoped<WhatsAppSignInReplyIssuer>();
        services.AddScoped<WhatsAppAttentionBotWriter>();
        services.AddScoped<PlatformWhatsAppBotWriter>();
        services.AddScoped<WhatsAppBotResolver>();
        services.AddScoped<WhatsAppModulePolicy>();
        services.AddScoped<WhatsAppBotReplyBuilder>();
        services.AddScoped<WhatsAppWebhookWriter>();
        services.AddScoped<IWhatsAppInboundService, WhatsAppInboundService>();
        services.AddScoped<WhatsAppInboundGuard>();
        services.AddScoped<ILoginCodeAdmission, WhatsAppCodeQuotaIssuer>();
        services.AddScoped<ILoginCodeChannel, WhatsAppLoginCodeChannel>();
        // Reemplaza la versión apagada del núcleo, que se registra antes con TryAdd (C12 de la E12).
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.StructuredData.IStructuredContactSource,
            WhatsAppStructuredContactSource>();
        services.AddScoped<IInvitationChannel, WhatsAppInvitationChannel>();
        services.AddScoped<IAccountNoticeChannel, WhatsAppAccountNoticeChannel>();
        services.AddScoped<IOutboxDeliveryObserver, WhatsAppDeliveryObserver>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity.IPhoneLinkObserver, WhatsAppPhoneLinkObserver>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Organization.IMemberAccessObserver, WhatsAppMemberAccessObserver>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal.IAccountDeletionParticipant, WhatsAppAccountDeletionParticipant>();
        services.AddSingleton<IErrorTextSource, WhatsAppErrorTextSource>();
        services.AddSingleton<IAuditTextSource, WhatsAppAuditTextSource>();
        services.AddSingleton<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features.IFeatureDefinitionContributor, WhatsAppFeatureDefinitionContributor>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features.IFeatureAvailabilityPolicy, WhatsAppAvailabilityPolicy>();
        services.AddScoped<IWhatsAppChannelBootstrapService, WhatsAppChannelBootstrapService>();
        services.AddScoped<WhatsAppChannelAuditWriter>();
        services.AddScoped<WhatsAppChannelCreator>();
        services.AddScoped<WhatsAppChannelUsageWriter>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Services.IPendingItemSource, WhatsAppNumberPendingItems>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features.IModuleHomeCardSource, WhatsAppModuleHomeCardSource>();
        services.AddScoped<IWhatsAppNumberService, WhatsAppNumberService>();
        services.AddScoped<IPlatformWhatsAppNumberService, PlatformWhatsAppNumberService>();
        services.AddScoped<IWhatsAppBotService, WhatsAppBotService>();
        services.AddScoped<IPlatformWhatsAppBotService, PlatformWhatsAppBotService>();
        services.AddScoped<IWhatsAppTeamService, WhatsAppTeamService>();
        services.AddScoped<IPlatformWhatsAppTeamService, PlatformWhatsAppTeamService>();
        services.AddScoped<IWhatsAppConversationService, WhatsAppConversationService>();
        services.AddScoped<IPlatformWhatsAppConversationService, PlatformWhatsAppConversationService>();
        services.AddScoped<WhatsAppSettingsResolver>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features.IModuleLimitContributor, WhatsAppModuleLimitContributor>();
        services.AddScoped<IWhatsAppSettingsService, WhatsAppSettingsService>();
        services.AddScoped<IPlatformWhatsAppSettingsService, PlatformWhatsAppSettingsService>();
        services.AddScoped<WhatsAppTeamResponseBuilder>();
        services.AddScoped<WhatsAppConversationResponseBuilder>();
        services.AddScoped<WhatsAppReplyIssuer>();
        services.AddScoped<PlatformWhatsAppOperatorResolver>();
        services.AddScoped<PlatformWhatsAppAuditWriter>();
        services.AddScoped<WhatsAppWaitingNotifier>();
        services.AddScoped<WhatsAppContactImportResolver>();
        services.AddScoped<WhatsAppConversationLinkWriter>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Engagement.IConsumerInteractionSource, WhatsAppConsumerInteractionSource>();
        services.AddScoped<IWhatsAppWaitingNoticeService, WhatsAppWaitingNoticeService>();
        services.AddScoped<IWhatsAppContactService, WhatsAppContactService>();
        services.AddScoped<IWhatsAppTemplateService, WhatsAppTemplateService>();
        services.AddScoped<IPlatformWhatsAppTemplateService, PlatformWhatsAppTemplateService>();
        services.AddScoped<IWhatsAppTemplateSyncService, WhatsAppTemplateSyncService>();
        services.AddScoped<WhatsAppTemplateIssuer>();
        services.AddScoped<WhatsAppTemplateStateWriter>();
        services.AddScoped<WhatsAppTemplateResponseBuilder>();
        services.AddScoped<WhatsAppCostResolver>();
        services.AddScoped<WhatsAppBroadcastChannelResolver>();
        services.AddScoped<WhatsAppBroadcastRecipientWriter>();
        services.AddScoped<WhatsAppAccountDataRemover>();
        services.AddScoped<IWhatsAppBroadcastDispatchService, WhatsAppBroadcastDispatchService>();
        services.AddScoped<IWhatsAppBroadcastService, WhatsAppBroadcastService>();
        services.AddScoped<IWhatsAppPhoneImportService, WhatsAppPhoneImportService>();
        services.AddScoped<WhatsAppPhoneImportResolver>();
        services.AddOptions<WhatsAppQuotaOptions>()
            .BindConfiguration(WhatsAppQuotaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<WhatsAppPhoneImportQuotaGuard>();
        // Los mismos topes para cada invitación por WhatsApp que emite el núcleo, como un reenvío desde Usuarios (B9).
        services.AddScoped<IInvitationAdmission>(provider => provider.GetRequiredService<WhatsAppPhoneImportQuotaGuard>());
        services.AddScoped<IPersonalWhatsAppService, PersonalWhatsAppService>();
        return services;
    }
}
