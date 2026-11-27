using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Readers;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Repositories;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms;

/// <summary>
/// Los adaptadores de Formularios (E12): la persistencia de los catálogos y la barrera de sus operaciones de archivo (C11); la
/// planilla llega con su tarea.
/// </summary>
public static class FormsModule
{
    public static IServiceCollection AddFormsModule(this IServiceCollection services)
    {
        services.AddSingleton<IPersistenceModelContributor, FormsModelContributor>();
        services.AddScoped<IFormCatalogRepository, FormCatalogRepository>();
        services.AddScoped<IFormCatalogReader, FormCatalogReader>();
        // La barrera abre su propia conexión, fuera de la transacción del servicio, como la reserva idempotente.
        services.AddScoped<IFormWorkLease>(provider => new FormWorkLeaseStore(
            provider.GetRequiredService<IConfiguration>().GetConnectionString("appdb")
                ?? throw new InvalidOperationException("The catalog file barrier needs the appdb connection."),
            provider.GetRequiredService<ITenantContext>(), provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IOptions<FormsOptions>>()));
        return services;
    }
}
