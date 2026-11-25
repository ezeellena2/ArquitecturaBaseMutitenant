using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using OpenTelemetry.Trace;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

public static class AssistantApiModule
{
    public const string ChatRoute = "Assistant.Chat";
    public const string ManagementRoute = "Assistant.Management";
    public static IServiceCollection AddAssistantModule(this IServiceCollection services)
    {
        services.AddSingleton<IInstalledModuleRouteAvailability, ChatRouteAvailability>();
        services.AddSingleton<IInstalledModuleRouteAvailability>(new RouteAvailability(ManagementRoute, true));
        services.AddSingleton<IHttpContractContributor, AssistantContractContributor>();
        services.AddScoped<AssistantNoticeReplayVerifier>();
        services.AddScoped<AssistantDurableAdapter>();
        services.AddScoped<AssistantNewKeyRateLimitFilter>();
        // ADR 0064 §4: los lugares de las subidas, uno por copia, que toma AssistantUploadSlot antes de leer el cuerpo.
        services.AddSingleton<AssistantUploadLimiter>();
        // 0064 §11: la actividad de cada turno, hija de la traza de quien lo pidió.
        services.ConfigureOpenTelemetryTracerProvider(tracing =>
            tracing.AddSource(ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AssistantTracing.SourceName));
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency.IIdempotentReplayResolver>(provider =>
            new AssistantReplayAdapter(provider.GetRequiredService<ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services.IAssistantDurableService>(), "assistant.conversation"));
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency.IIdempotentReplayResolver>(provider =>
            new AssistantReplayAdapter(provider.GetRequiredService<ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services.IAssistantDurableService>(), "assistant.quick"));
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency.IIdempotentReplayResolver>(provider =>
            new AssistantReplayAdapter(provider.GetRequiredService<ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services.IAssistantDurableService>(), "assistant.turn"));
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency.IIdempotentReplayResolver>(provider =>
            new AssistantReplayAdapter(provider.GetRequiredService<ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services.IAssistantDurableService>(), "assistant.attachment"));
        return services;
    }
    private sealed record RouteAvailability(string Key, bool IsAvailable) : IInstalledModuleRouteAvailability;
    private sealed class ChatRouteAvailability(IConfiguration configuration) : IInstalledModuleRouteAvailability
    {
        public string Key => ChatRoute;
        public bool IsAvailable => !bool.TryParse(configuration["FeatureManagement:assistant"], out var enabled) || enabled;
    }
}
