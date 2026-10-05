using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

/// <summary>Registra el perfil de la persona y sus preferencias.</summary>
internal static class ProfileRegistration
{
    internal static IServiceCollection AddProfile(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ProfilePreferencesResolver>();
        services.AddScoped<ProfileSnapshotBuilder>();
        services.AddScoped<ProfileEffectivePermissionsBuilder>();
        return services;
    }
}
