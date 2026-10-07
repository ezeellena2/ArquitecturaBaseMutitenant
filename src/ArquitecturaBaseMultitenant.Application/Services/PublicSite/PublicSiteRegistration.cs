using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.PublicSite;

/// <summary>Registra la página pública de la organización, con su fuente de «Para resolver».</summary>
internal static class PublicSiteRegistration
{
    internal static IServiceCollection AddPublicSite(this IServiceCollection services)
    {
        services.AddScoped<IPendingItemSource, PublicPagePendingItems>();
        services.AddScoped<IPublicPageService, PublicPageService>();
        services.AddScoped<IPublicSiteService, PublicSiteService>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<PublicPageEditor>();
        return services;
    }
}
