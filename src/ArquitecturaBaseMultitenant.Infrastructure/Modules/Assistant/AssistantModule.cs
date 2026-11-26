using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
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
        // ADR 0068 §5: las herramientas y las fuentes corren en un alcance propio y READ ONLY.
        services.AddScoped<IAssistantReadOnlyScope, AssistantReadOnlyScope>();
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
        // 0064 §11: el anillo y su control; una copia que no coincide no queda lista («ai-keyring») ni toma turnos, y con
        // IKeyRingReadiness tampoco toma nada de la cola de salida ni de las de los canales.
        services.AddScoped<IAiKeyRingControlRepository, AiKeyRingControlRepository>();
        services.AddScoped<IAiKeyRingControl, HmacAiKeyRingControl>();
        services.AddSingleton<AiKeyRingReadiness>();
        services.AddSingleton<IKeyRingReadiness>(provider => provider.GetRequiredService<AiKeyRingReadiness>());
        services.AddHealthChecks().AddCheck<AiKeyRingHealthCheck>(AiKeyRingHealthCheck.Name);
        // E9: transporte nombrado de los clientes de Anthropic, sin los reintentos de ServiceDefaults ni loggers HTTP que vean la clave.
        // La API de reemplazo sigue experimental en Extensions 10; limita la supresión a esta composición.
#pragma warning disable EXTEXP0001
        // 0064 §7 (B13): 10 s para conectarse; vencidos, no se mandó nada y la falla es no disponible. Una redirección no se
        // sigue: repetiría el pedido pago y llevaría x-api-key a otro host; queda como rechazo. Como ArquitecturaBase.
        services.AddHttpClient(AnthropicChatClientBuilder.TransportName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = AssistantProviderConnectHandler.ConnectTimeout,
            })
            .AddHttpMessageHandler(() => new AssistantProviderConnectHandler())
            .RemoveAllLoggers().RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        services.AddScoped<AnthropicChatClientBuilder>();
        services.AddScoped<IChatClientFactory, AnthropicChatClientFactory>();
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
