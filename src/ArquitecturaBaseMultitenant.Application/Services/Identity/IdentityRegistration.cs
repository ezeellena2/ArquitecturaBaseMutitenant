using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Registra los métodos de ingreso de la cuenta y los avisos a la cuenta.</summary>
internal static class IdentityRegistration
{
    internal static IServiceCollection AddIdentity(this IServiceCollection services)
    {
        services.AddScoped<AccountNoticeIssuer>();
        services.AddScoped<ILoginMethodManagementService, LoginMethodManagementService>();
        services.AddScoped<LoginMethodGuard>();
        services.AddScoped<LoginMethodIssuer>();
        services.AddScoped<LoginMethodVerifier>();
        services.AddScoped<LoginMethodNotifier>();
        services.AddScoped<GoogleMethodLinkFinalizer>();
        services.AddScoped<LoginMethodAvailabilityPolicy>();
        services.AddScoped<LoginMethodEditor>();
        services.AddScoped<IAccountGoogleService, AccountGoogleService>();
        services.AddScoped<GoogleMethodLinker>();
        services.AddScoped<IAccountLoginMethodsService, AccountLoginMethodsService>();
        return services;
    }
}
