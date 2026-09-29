using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

internal static class PersistenceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantContext>(provider => new TenantContext(() =>
            provider.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction is not null));
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantScope>(provider => provider.GetRequiredService<TenantContext>());
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<TenantStampInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditTrailInterceptor>();
        services.AddScoped<IAuditLog, AuditLog>();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(configuration.GetConnectionString("appdb")
                    ?? throw new InvalidOperationException(
                        "Missing connection string 'ConnectionStrings:appdb'. Start the API from the AppHost."))
                .AddInterceptors(
                    provider.GetRequiredService<TenantConnectionInterceptor>(),
                    provider.GetRequiredService<TenantStampInterceptor>(),
                    provider.GetRequiredService<SoftDeleteInterceptor>(),
                    provider.GetRequiredService<AuditableEntityInterceptor>(),
                    provider.GetRequiredService<AuditTrailInterceptor>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
