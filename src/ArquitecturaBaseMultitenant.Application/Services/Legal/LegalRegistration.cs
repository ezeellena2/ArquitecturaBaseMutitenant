using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Registra los documentos legales y su aceptación, la baja de la cuenta y la regla de la última Dueña.</summary>
internal static class LegalRegistration
{
    internal static IServiceCollection AddLegal(this IServiceCollection services)
    {
        services.AddScoped<ILastTenantAdminGuard, LastTenantAdminGuard>();
        services.AddScoped<ILegalService, LegalService>();
        services.AddScoped<ILegalAcceptanceService, LegalAcceptanceService>();
        services.AddScoped<LegalAcceptanceGuard>();
        services.AddScoped<LegalAcceptanceWriter>();
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        services.AddScoped<AccountDeletionRequester>();
        services.AddScoped<AccountDeletionScheduler>();
        services.AddScoped<AccountDeletionNotifier>();
        services.AddScoped<AccountDeletionCanceller>();
        services.AddScoped<AccountDeletionSessionRestorer>();
        services.AddScoped<IAccountDeletionProcessingService, AccountDeletionProcessingService>();
        services.AddScoped<AccountDeletionProcessor>();
        services.AddScoped<AccountDeletionCancelIssuer>();
        return services;
    }
}
