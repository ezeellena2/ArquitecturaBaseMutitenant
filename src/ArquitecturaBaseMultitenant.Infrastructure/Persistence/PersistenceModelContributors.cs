using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Descubrimiento técnico para scaffolding/bootstrap y fábricas sin DI; nunca lee opciones ni abre red.</summary>
public static class PersistenceModelContributors
{
    public static IReadOnlyList<IPersistenceModelContributor> Discover() =>
        [.. typeof(ApplicationDbContext).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && typeof(IPersistenceModelContributor).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (IPersistenceModelContributor)Activator.CreateInstance(type, nonPublic: true)!)];

    public static IReadOnlyDictionary<Type, string> GlobalEntities(IEnumerable<IPersistenceModelContributor> contributors)
    {
        var result = new Dictionary<Type, string>();
        foreach (var contributor in contributors)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contributor.ConfigurationNamespace);
            foreach (var (type, schema) in contributor.GlobalEntities)
            {
                if (schema is not (Schemas.Identity or Schemas.Platform) || !result.TryAdd(type, schema))
                    throw new InvalidOperationException($"Invalid or duplicate global declaration: {type.FullName}.");
            }
        }
        return result;
    }
}

/// <summary>Separar el modelo sin módulos del completo aunque se comparta el proveedor EF.</summary>
internal sealed class PersistenceModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => context is ApplicationDbContext application
        ? (context.GetType(), application.ContributorCacheKey, designTime)
        : (object)(context.GetType(), designTime);
}
