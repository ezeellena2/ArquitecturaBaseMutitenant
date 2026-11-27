using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms;

/// <summary>Los adaptadores de Formularios (E12): la persistencia y la planilla llegan con sus tareas.</summary>
public static class FormsModule
{
    public static IServiceCollection AddFormsModule(this IServiceCollection services) => services;
}
