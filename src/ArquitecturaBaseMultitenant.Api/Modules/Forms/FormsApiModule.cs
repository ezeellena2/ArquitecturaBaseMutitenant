using ArquitecturaBaseMultitenant.Api.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms;

/// <summary>Las rutas de Formularios (E12), detrás de la funcionalidad <c>forms</c>.</summary>
public static class FormsApiModule
{
    public static IServiceCollection AddFormsModule(this IServiceCollection services)
    {
        services.AddSingleton<IHttpContractContributor, FormsContractContributor>();
        return services;
    }
}
