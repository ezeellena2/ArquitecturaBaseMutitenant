using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.OpenApi;

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
        return services;
    }
    private sealed record RouteAvailability(string Key, bool IsAvailable) : IInstalledModuleRouteAvailability;
    private sealed class ChatRouteAvailability(IConfiguration configuration) : IInstalledModuleRouteAvailability
    {
        public string Key => ChatRoute;
        public bool IsAvailable => !bool.TryParse(configuration["FeatureManagement:assistant"], out var enabled) || enabled;
    }
}
