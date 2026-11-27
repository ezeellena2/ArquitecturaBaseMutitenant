using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence;

/// <summary>Las tablas de Formularios (E12): todas privadas de la organización, sin entidades globales.</summary>
internal sealed class FormsModelContributor : IPersistenceModelContributor
{
    public string ConfigurationNamespace => typeof(Configurations.FormCatalogConfiguration).Namespace!;
    public IReadOnlyDictionary<Type, string> GlobalEntities { get; } = new Dictionary<Type, string>();
    public void Configure(ModelBuilder builder) => builder.ApplyConfigurationsFromAssembly(GetType().Assembly,
        type => type.Namespace == ConfigurationNamespace);
}
