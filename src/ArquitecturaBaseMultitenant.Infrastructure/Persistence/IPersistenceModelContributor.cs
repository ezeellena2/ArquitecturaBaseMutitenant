using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Aporta configuraciones y declaraciones globales explícitas sin depender de configuración o secretos.</summary>
public interface IPersistenceModelContributor
{
    string ConfigurationNamespace { get; }
    IReadOnlyDictionary<Type, string> GlobalEntities { get; }
    void Configure(ModelBuilder builder);
}
