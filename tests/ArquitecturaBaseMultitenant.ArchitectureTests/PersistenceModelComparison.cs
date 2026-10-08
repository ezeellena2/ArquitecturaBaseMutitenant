using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArquitecturaBaseMultitenant.TestSupport;

/// <summary>Compara el esquema completo y los metadatos EF que las migraciones no representan.</summary>
internal static class PersistenceModelComparison
{
    public static bool HasDifferences(DbContext source, DbContext target)
    {
        var sourceModel = source.GetService<IDesignTimeModel>().Model;
        var targetModel = target.GetService<IDesignTimeModel>().Model;
        return source.GetService<IMigrationsModelDiffer>().HasDifferences(
                sourceModel.GetRelationalModel(), targetModel.GetRelationalModel())
            || !NonRelationalMetadata(sourceModel).SequenceEqual(NonRelationalMetadata(targetModel), StringComparer.Ordinal);
    }

    public static void AssertEqual(DbContext source, DbContext target) =>
        Assert.False(HasDifferences(source, target),
            "Models differ in their relational schema, EF metadata or query filters.");

    private static string[] NonRelationalMetadata(IModel model) =>
    [
        // Incluye CLR types, claves, relaciones, nullable, concurrency/value generation, convertidores,
        // access modes y annotations. El diff relacional cubre además índices/FK/columnas/constraints.
        model.ToDebugString(MetadataDebugStringOptions.LongDefault),
        .. model.GetEntityTypes().OrderBy(entity => entity.Name, StringComparer.Ordinal)
            .SelectMany(entity => entity.GetDeclaredQueryFilters().OrderBy(filter => filter.Key, StringComparer.Ordinal)
                .Select(filter => entity.Name + ":" + filter.Key + ":" + filter.IsAnonymous + ":" + filter.Expression)),
    ];
}
