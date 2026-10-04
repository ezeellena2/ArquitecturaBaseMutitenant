using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Registra las empresas de la organización y sus miembros.</summary>
internal static class CompaniesRegistration
{
    internal static IServiceCollection AddCompanies(this IServiceCollection services)
    {
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ICompanyMembershipService, CompanyMembershipService>();
        services.AddScoped<CompanyCreator>();
        services.AddScoped<CompanyReferenceDataResolver>();
        services.AddScoped<CompanyMembershipWriter>();
        services.AddScoped<ILastCompanyAdminGuard, LastCompanyAdminGuard>();
        return services;
    }
}
