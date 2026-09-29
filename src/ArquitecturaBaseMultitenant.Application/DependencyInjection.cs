using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
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
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IConnectService, ConnectService>();
        services.AddScoped<ILegalService, LegalService>();
        services.AddScoped<SignupPolicy>();
        services.AddScoped<IPersonalSpaceProvisioner, PersonalSpaceProvisioner>();
        services.AddScoped<LoginCodeIssuer>();
        services.AddScoped<LoginCodeVerifier>();
        services.AddScoped<UserCultures>();
        services.AddScoped<DisplayFormatter>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<IRequestValidator, RequestValidator>();

        return services;
    }
}
