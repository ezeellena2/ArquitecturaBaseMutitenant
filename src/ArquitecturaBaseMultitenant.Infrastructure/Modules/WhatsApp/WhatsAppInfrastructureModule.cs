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
/// <summary>La capa registra sus adaptadores. PhoneNumberId controla el canal; el esquema siempre se aporta.</summary>
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
        services.AddHostedService<Retention.WhatsAppMessageRetentionService>();
        services.AddScoped<IWhatsAppContactRepository, WhatsAppContactRepository>();
        services.AddScoped<IWhatsAppMessageRepository, WhatsAppMessageRepository>();
        services.AddScoped<IWhatsAppCodeQuotaRepository, WhatsAppCodeQuotaRepository>();
        services.AddSingleton<IWhatsAppChannelSettings, WhatsAppChannelSettings>();
        services.AddSingleton<WhatsAppTemplateCatalog>();
        services.AddScoped<IWhatsAppTemplateRenderer, WhatsAppTemplateRenderer>();
        if (!string.IsNullOrWhiteSpace(configuration.GetSection(WhatsAppOptions.SectionName)[nameof(WhatsAppOptions.PhoneNumberId)]))
        {
            // La API de reemplazo sigue experimental en Extensions 10; limita la supresión a esta composición.
#pragma warning disable EXTEXP0001
            services.AddHttpClient<WhatsAppCloudClient>(client => { client.BaseAddress = new Uri("https://graph.facebook.com/"); client.Timeout = TimeSpan.FromSeconds(60); })
                .RemoveAllLoggers()
                .RemoveAllResilienceHandlers()
                .AddStandardResilienceHandler(resilience => resilience.Retry.DisableForUnsafeHttpMethods());
#pragma warning restore EXTEXP0001
            services.AddScoped<IChannelSender, WhatsAppChannelSender>();
        }
        var section = configuration.GetSection(WhatsAppOptions.SectionName);
        var enabled = !string.IsNullOrWhiteSpace(section[nameof(WhatsAppOptions.PhoneNumberId)]);
        var webhook = enabled && !string.IsNullOrWhiteSpace(section[nameof(WhatsAppOptions.AppSecret)])
            && !string.IsNullOrWhiteSpace(section[nameof(WhatsAppOptions.VerifyToken)]);
        services.AddOptions<WhatsAppOptions>().Bind(section).Configure(options =>
        {
            var countries = section.GetSection(nameof(WhatsAppOptions.AllowedCountries));
            // Bind appends to initialized arrays; configured countries replace the default.
            if (countries.Exists()) options.AllowedCountries = countries.Get<string[]>() ?? [];
        }).ValidateDataAnnotations();
        if (enabled && System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is not "GetDocument.Insider")
            services.AddOptions<WhatsAppOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<WhatsAppOptions>, WhatsAppOptionsValidator>();
        if (enabled) services.AddSingleton<IWhatsAppAvailability>(new WhatsAppAvailability(true, webhook));
        else services.AddSingleton<IWhatsAppAvailability, DisabledWhatsAppAvailability>();
        services.AddHealthChecks().AddCheck<WhatsAppHealthCheck>("whatsapp");
        if (enabled && !webhook) services.AddHostedService<WhatsAppWebhookOffNotice>();
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
