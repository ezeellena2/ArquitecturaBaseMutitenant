using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Profile;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IOutboxDispatchService, OutboxDispatchService>();
        services.AddScoped<ILoginCodeService, LoginCodeService>();
        services.AddScoped<LoginCodeRequester>();
        services.AddScoped<LoginCodeVerificationFlow>();
        services.AddScoped<ILoginMethodsService, LoginMethodsService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<SignupCodeRequester>();
        services.AddScoped<SignupVerificationFlow>();
        services.AddScoped<SignupVerificationCore>();
        services.AddScoped<SignupAccountRegistrar>();
        services.AddScoped<SignupExistingMethodVerifier>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();
        services.AddScoped<GoogleAccountResolver>();
        services.AddScoped<GoogleAccountRegistrar>();
        services.AddScoped<IConnectService, ConnectService>();
        services.AddScoped<IConnectAuthorizationService, ConnectAuthorizationService>();
        services.AddScoped<IConnectLogoutService, ConnectLogoutService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ProfileSnapshotBuilder>();
        services.AddScoped<ILegalService, LegalService>();
        services.AddScoped<ILegalAcceptanceService, LegalAcceptanceService>();
        services.AddScoped<LegalAcceptanceGuard>();
        services.AddScoped<LegalAcceptanceWriter>();
        services.AddScoped<SignupPolicy>();
        services.AddScoped<IPersonalSpaceProvisioner, PersonalSpaceProvisioner>();
        services.AddScoped<TenantSpaceProvisioner>();
        services.AddScoped<LoginCodeIssuer>();
        services.AddScoped<LoginCodeVerifier>();
        services.AddScoped<UserCultures>();
        services.AddScoped<AccountNoticeIssuer>();
        services.AddScoped<ILoginMethodManagementService, LoginMethodManagementService>();
        services.AddScoped<LoginMethodGuard>();
        services.AddScoped<LoginMethodIssuer>();
        services.AddScoped<LoginMethodVerifier>();
        services.AddScoped<LoginMethodNotifier>();
        services.AddScoped<LoginMethodAvailability>();
        services.AddScoped<LoginMethodChanger>();
        services.AddScoped<IReauthService, ReauthService>();
        services.AddScoped<ReauthIssuer>();
        services.AddScoped<ReauthVerifier>();
        services.AddScoped<ReauthTicketConsumer>();
        services.AddScoped<IAccountGoogleService, AccountGoogleService>();
        services.AddScoped<GoogleMethodLinker>();
        services.AddScoped<IAccountLoginMethodsService, AccountLoginMethodsService>();
        services.AddScoped<DisplayFormatter>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<IRequestValidator, RequestValidator>();

        return services;
    }
}
