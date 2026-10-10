using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

public static class AssistantApiModule
{
    public const string ChatRoute = "Assistant.Chat";
    public const string ManagementRoute = "Assistant.Management";
    public static IServiceCollection AddAssistantModule(this IServiceCollection services)
    {
        services.AddSingleton<IInstalledModuleRouteAvailability>(new RouteAvailability(ChatRoute, false));
        services.AddSingleton<IInstalledModuleRouteAvailability>(new RouteAvailability(ManagementRoute, true));
        services.AddSingleton<IHttpContractContributor, AssistantContractContributor>();
        return services;
    }
    private sealed record RouteAvailability(string Key, bool IsAvailable) : IInstalledModuleRouteAvailability;
}
