using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Readers;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Repositories;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms;

/// <summary>Los adaptadores de Formularios (E12): la persistencia de los catálogos; la planilla llega con su tarea.</summary>
public static class FormsModule
{
    public static IServiceCollection AddFormsModule(this IServiceCollection services)
    {
        services.AddSingleton<IPersistenceModelContributor, FormsModelContributor>();
        services.AddScoped<IFormCatalogRepository, FormCatalogRepository>();
        services.AddScoped<IFormCatalogReader, FormCatalogReader>();
        return services;
    }
}
