using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Disabled;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using Microsoft.Extensions.Http.Resilience;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
/// <summary>
/// La capa registra sus adaptadores; el esquema siempre se aporta. Desde la E11 el canal de Plataforma sale del número
/// conectado (la configuración solo lo crea al arrancar) y el webhook depende de sus dos secretos.
/// </summary>
public static class WhatsAppInfrastructureModule
{
    public static IServiceCollection AddWhatsAppModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IPersistenceModelContributor, WhatsAppModelContributor>();
        services.AddSingleton<IWhatsAppSignatureValidator, Webhook.WhatsAppSignatureValidator>();
        services.AddScoped<IWhatsAppWebhookReader, Webhook.WhatsAppWebhookReader>();
        services.AddSingleton<IWhatsAppWebhookReplay, Webhook.WhatsAppWebhookReplay>();
        services.AddSingleton<Inbound.WhatsAppInboundSignal>();
        services.AddSingleton<IWhatsAppInboundSignal>(provider => provider.GetRequiredService<Inbound.WhatsAppInboundSignal>());
        services.AddSingleton<IWhatsAppInboundSettings, Inbound.WhatsAppInboundSettings>();
        services.AddHostedService<Inbound.WhatsAppInboundProcessor>();
        services.AddHostedService<Retention.WhatsAppMessageRetentionWorker>();
        services.AddHostedService<WhatsAppWaitingNoticeWorker>();
        services.AddHostedService<WhatsAppTemplateSyncWorker>();
        services.AddHostedService<WhatsAppBroadcastWorker>();
        services.AddScoped<IWhatsAppBroadcastRepository, WhatsAppBroadcastRepository>();
        services.AddScoped<IWhatsAppBroadcastReader, WhatsAppBroadcastReader>();
        services.AddSingleton<IWhatsAppSystemTemplates>(provider => provider.GetRequiredService<WhatsAppTemplateCatalog>());
        services.AddScoped<IWhatsAppTemplateRepository, WhatsAppTemplateRepository>();
        services.AddScoped<IPlatformWhatsAppTemplateRepository, PlatformWhatsAppTemplateRepository>();
        services.AddScoped<IWhatsAppTemplateStateRepository, WhatsAppTemplateStateRepository>();
        services.AddScoped<IWhatsAppTemplateReader, WhatsAppTemplateReader>();
        services.AddScoped<IWhatsAppRateReader, WhatsAppRateReader>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Persistence.IModuleDataSeedContributor, WhatsAppRateSeed>();
        services.AddScoped<IWhatsAppContactRepository, WhatsAppContactRepository>();
        services.AddScoped<IWhatsAppMessageRepository, WhatsAppMessageRepository>();
        services.AddScoped<IWhatsAppCodeQuotaRepository, WhatsAppCodeQuotaRepository>();
        services.AddSingleton<IWhatsAppQuotaIpHasher, WhatsAppQuotaIpHasher>();
        services.AddSingleton<IWhatsAppChannelSettings, WhatsAppChannelSettings>();
        services.AddSingleton<WhatsAppTemplateCatalog>();
        services.AddScoped<IWhatsAppTemplateRenderer, WhatsAppTemplateRenderer>();
        services.AddScoped<IWhatsAppChannelRepository, WhatsAppChannelRepository>();
        services.AddScoped<IWhatsAppChannelReader, WhatsAppChannelReader>();
        services.AddScoped<IWhatsAppChatRepository, WhatsAppChatRepository>();
        services.AddScoped<IPlatformWhatsAppChatRepository, PlatformWhatsAppChatRepository>();
        services.AddScoped<IWhatsAppCustomerRepository, WhatsAppCustomerRepository>();
        services.AddScoped<IWhatsAppBotRepository, WhatsAppBotRepository>();
        services.AddScoped<IPlatformWhatsAppBotRepository, PlatformWhatsAppBotRepository>();
        services.AddScoped<IWhatsAppBotReader, WhatsAppBotReader>();
        services.AddScoped<IWhatsAppTeamRepository, WhatsAppTeamRepository>();
        services.AddScoped<IPlatformWhatsAppTeamRepository, PlatformWhatsAppTeamRepository>();
        services.AddScoped<IWhatsAppTeamReader, WhatsAppTeamReader>();
        services.AddScoped<IWhatsAppConversationReader, WhatsAppConversationReader>();
        services.AddScoped<IWhatsAppTeamNoticeRepository, WhatsAppTeamNoticeRepository>();
        services.AddScoped<IWhatsAppContactReader, WhatsAppContactReader>();
        services.AddScoped<IWhatsAppConversationLinkRepository, WhatsAppConversationLinkRepository>();
        services.AddScoped<IWhatsAppConversationLinkReader, WhatsAppConversationLinkReader>();
        services.AddSingleton<Application.Modules.WhatsApp.Interfaces.Integrations.IWhatsAppContactSpreadsheet, WhatsAppContactSpreadsheet>();
        services.AddScoped<Application.Modules.WhatsApp.Interfaces.Integrations.IWhatsAppWaitingEmailRenderer, Email.WhatsAppWaitingEmailRenderer>();
        services.AddSingleton<IWhatsAppTokenProtector, WhatsAppTokenProtector>();
        services.AddScoped<WhatsAppChannelCredentials>();
        services.AddSingleton<WhatsAppPlatformChannelState>();
        services.AddSingleton<IWhatsAppChannelCache>(provider => provider.GetRequiredService<WhatsAppPlatformChannelState>());
        services.AddHostedService<WhatsAppPlatformChannelWorker>();
        // Los números de las organizaciones envían aunque Plataforma no tenga uno (decisión 7 de la E11).
        // La API de reemplazo sigue experimental en Extensions 10; limita la supresión a esta composición.
#pragma warning disable EXTEXP0001
        services.AddHttpClient<WhatsAppCloudClient>(client => { client.BaseAddress = new Uri("https://graph.facebook.com/"); client.Timeout = TimeSpan.FromSeconds(60); })
            .RemoveAllLoggers()
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(resilience => resilience.Retry.DisableForUnsafeHttpMethods());
        // El canje del alta lleva el secreto de la app y el código en la consulta: no registra URLs ni reintenta.
        services.AddHttpClient<IWhatsAppGraphClient, WhatsAppGraphClient>(client => { client.BaseAddress = new Uri("https://graph.facebook.com/"); client.Timeout = TimeSpan.FromSeconds(30); })
            .RemoveAllLoggers()
            .RemoveAllResilienceHandlers();
        // Las plantillas: crear y borrar no se reintentan; la lista, sí, como cualquier GET.
        services.AddHttpClient<IWhatsAppTemplateClient, WhatsAppTemplateClient>(client => { client.BaseAddress = new Uri("https://graph.facebook.com/"); client.Timeout = TimeSpan.FromSeconds(30); })
            .RemoveAllLoggers()
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(resilience => resilience.Retry.DisableForUnsafeHttpMethods());
#pragma warning restore EXTEXP0001
        services.AddScoped<IChannelSender, WhatsAppChannelSender>();
        var section = configuration.GetSection(WhatsAppOptions.SectionName);
        var configured = !string.IsNullOrWhiteSpace(section[nameof(WhatsAppOptions.PhoneNumberId)]);
        var webhook = !string.IsNullOrWhiteSpace(section[nameof(WhatsAppOptions.AppSecret)])
            && !string.IsNullOrWhiteSpace(section[nameof(WhatsAppOptions.VerifyToken)]);
        var exporter = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider";
        services.AddOptions<WhatsAppOptions>().Bind(section).Configure(options =>
        {
            var countries = section.GetSection(nameof(WhatsAppOptions.AllowedCountries));
            // Bind appends to initialized arrays; configured countries replace the default.
            if (countries.Exists()) options.AllowedCountries = countries.Get<string[]>() ?? [];
        }).ValidateDataAnnotations();
        if (configured && !exporter)
            services.AddOptions<WhatsAppOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<WhatsAppOptions>, WhatsAppOptionsValidator>();
        // El exportador de OpenAPI arma el host sin base: ahí el canal queda apagado.
        if (exporter) services.AddSingleton<IWhatsAppAvailability, DisabledWhatsAppAvailability>();
        else services.AddSingleton<IWhatsAppAvailability, WhatsAppAvailability>();
        services.AddHealthChecks().AddCheck<WhatsAppHealthCheck>("whatsapp");
        if (configured && !webhook) services.AddHostedService<WhatsAppWebhookOffNotice>();
        return services;
    }
}
internal sealed partial class WhatsAppWebhookOffNotice(ILogger<WhatsAppWebhookOffNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) { WebhookDisabled(logger); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    [LoggerMessage(Level = LogLevel.Warning, Message = "WhatsApp webhook disabled: configure WhatsApp:AppSecret and WhatsApp:VerifyToken together.")]
    private static partial void WebhookDisabled(ILogger logger);
}
