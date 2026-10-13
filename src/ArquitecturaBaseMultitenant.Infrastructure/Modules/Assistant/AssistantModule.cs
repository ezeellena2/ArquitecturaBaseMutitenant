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
        services.AddScoped<IAssistantTurnRepository, AssistantTurnRepository>();
        services.AddScoped<AssistantTurnStore, TenantAssistantTurnStore>();
        services.AddScoped<AssistantTurnStore, PlatformAssistantTurnStore>();
        services.AddScoped<IAssistantChatContext, AssistantChatContext>();
        services.AddHostedService<AssistantTurnWorker>();
        services.AddScoped<IAssistantConversationReader, AssistantConversationReader>();
        services.AddScoped<IAssistantNoticeReader, AssistantNoticeReader>();
        services.AddScoped<IAssistantNoticeRepository, AssistantNoticeRepository>();
        services.AddScoped<IModuleDataSeedContributor, AssistantLegalDocumentSeed>();
        services.AddScoped<IAssistantConversationRepository, AssistantConversationRepository>();
        services.AddScoped<IAssistantDurableRepository, AssistantDurableRepository>();
        services.AddScoped<IAssistantSnapshotRetentionRepository, AssistantSnapshotRetentionRepository>();
        services.AddHostedService<Retention.AssistantSnapshotRetentionWorker>();
        services.AddScoped<IAssistantAccountDeletionCleanupRepository, AssistantAccountDeletionCleanupRepository>();
        services.AddScoped<IPlatformAssistantConversationRepository, PlatformAssistantConversationRepository>();
        services.AddScoped<IAssistantSettingsRepository, AssistantSettingsRepository>();
        services.AddScoped<IAssistantModelRepository, AssistantModelRepository>();
        services.AddScoped<IAssistantModelReader, AssistantModelReader>();
        services.AddScoped<IAssistantFinanceRepository, AssistantFinanceRepository>();
        services.AddScoped<IAssistantBudgetAlertRepository, AssistantBudgetAlertRepository>();
        services.AddHostedService<AssistantBudgetAlertWorker>();
        services.AddScoped<IAssistantBudgetEmailRenderer, Email.AssistantBudgetEmailRenderer>();
        services.AddScoped<IAssistantFinanceReader, AssistantFinanceReader>();
        services.AddScoped<IAssistantUsageReader, AssistantUsageReader>();
        services.AddSingleton<IAssistantProviderCatalog, AssistantProviderCatalog>();
        services.AddScoped<IAssistantKeyProtector, AssistantKeyProtector>();
        services.AddScoped<IChatClientFactory, AnthropicChatClientFactory>();
        services.AddSingleton<AssistantFakeChatMetrics>();
        services.AddScoped<IAssistantAttachmentRepository, AssistantAttachmentRepository>();
        services.AddScoped<IAssistantAttachmentContentReader, AssistantAttachmentContentReader>();
        services.AddScoped<IAssistantExportRenderer, Export.AssistantExportRenderer>();
        services.AddScoped<IAssistantRetentionRepository, AssistantRetentionRepository>();
        services.AddHostedService<Retention.AssistantRetentionWorker>();
        services.AddSingleton<IValidateOptions<AssistantOptions>, AssistantOptionsValidator>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Help.IHelpArticleReader,
            Help.RepositoryHelpArticleReader>();
        services.AddScoped<IModuleDataSeedContributor, AssistantReferenceDataSeed>();
        return services;
    }
}
