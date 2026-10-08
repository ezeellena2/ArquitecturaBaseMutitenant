using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Registra el ingreso, el registro de personas y de empresas, Google, la sesión y «Recuperar mi cuenta», con las opciones del código de ingreso.</summary>
internal static class AuthRegistration
{
    internal static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services.AddOptions<LoginCodeOptions>()
            .BindConfiguration(LoginCodeOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ITenantSlugAvailabilityService, TenantSlugAvailabilityService>();
        services.AddScoped<ILoginCodeService, LoginCodeService>();
        services.AddScoped<ILoginLinkService, LoginLinkService>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity.ILoginLinkIssuer, LoginLinkIssuer>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddScoped<IAccountRecoveryNoticeService, AccountRecoveryNoticeService>();
        services.AddScoped<AccountRecoveryNotifier>();
        services.AddScoped<AccountRecoveryGuard>();
        services.AddScoped<LoginDestinationResolver>();
        services.AddScoped<LoginCodeRequester>();
        services.AddScoped<LoginCodeDeliveryIssuer>();
        services.AddScoped<LoginInvitationResolver>();
        services.AddScoped<LoginVerifier>();
        services.AddScoped<LoginCodeAttemptGuard>();
        services.AddScoped<LoginCodeAttemptVerifier>();
        services.AddScoped<ILoginMethodsService, LoginMethodsService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IBusinessSignupService, BusinessSignupService>();
        services.AddScoped<BusinessOrganizationCreator>();
        services.AddScoped<BusinessSignupPolicy>();
        services.AddScoped<SignupCodeRequester>();
        services.AddScoped<SignupCodeVerifier>();
        services.AddScoped<SignupAccountRegistrar>();
        services.AddScoped<SignupExistingMethodVerifier>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();
        services.AddScoped<GoogleAccountResolver>();
        services.AddScoped<GoogleAccountMethodFinalizer>();
        services.AddScoped<GoogleAccountGuard>();
        services.AddScoped<GoogleAccountRegistrar>();
        services.AddScoped<IConnectService, ConnectService>();
        services.AddScoped<IConnectAuthorizationService, ConnectAuthorizationService>();
        services.AddScoped<IConnectLogoutService, ConnectLogoutService>();
        services.AddScoped<LoginAccountGuard>();
        services.AddScoped<SignupPolicy>();
        services.AddScoped<IPersonalSpaceProvisioner, PersonalSpaceProvisioner>();
        services.AddScoped<TenantSpaceProvisioner>();
        services.AddScoped<TenantProvisioningReplayVerifier>();
        services.AddScoped<TenantProvisioner>();
        services.AddScoped<LoginCodeIssuer>();
        services.AddScoped<LoginCodeVerifier>();
        services.AddScoped<UserCultureResolver>();
        return services;
    }
}
