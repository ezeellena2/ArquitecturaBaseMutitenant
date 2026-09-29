using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

internal static class PersistenceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantContext>(provider => new TenantContext(() =>
            provider.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction is not null));
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantScope>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<TenantStampInterceptor>();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(configuration.GetConnectionString("appdb")
                    ?? throw new InvalidOperationException(
                        "Missing connection string 'ConnectionStrings:appdb'. Start the API from the AppHost."))
                .AddInterceptors(
                    provider.GetRequiredService<TenantConnectionInterceptor>(),
                    provider.GetRequiredService<TenantStampInterceptor>()));

        return services;
    }
}
