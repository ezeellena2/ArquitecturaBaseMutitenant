using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Ningún cambio del modelo de EF queda fuera de una migración (traspaso 18, 04). Dos desarrollos que generan migraciones
/// en paralelo pueden dejar en el snapshot el bloque de una entidad sin la entidad ni su migración, o una migración sin
/// su Designer, que EF no descubre y saltea. Con EF 10, aplicar las migraciones al arrancar frena la app si el modelo
/// tiene cambios pendientes. No abre conexiones: compara el modelo, el snapshot y las migraciones compiladas.
/// </summary>
public sealed class ModelSnapshotTests
{
    [Fact]
    public void The_model_has_no_changes_outside_the_snapshot()
    {
        // El mismo contexto con que se generan las migraciones (dotnet ef) y que el arranque migra: núcleo y módulos.
        using var context = new DesignTimeApplicationDbContextFactory().CreateDbContext([]);

        Assert.False(context.Database.HasPendingModelChanges(),
            "The EF model has changes that are not in a migration: regenerate it on top of the last one that entered.");
    }

    [Fact]
    public void The_snapshot_is_the_target_model_of_the_last_migration()
    {
        using var context = new DesignTimeApplicationDbContextFactory().CreateDbContext([]);
        var assembly = context.GetService<IMigrationsAssembly>();
        var last = assembly.Migrations.Last();
        var migration = assembly.CreateMigration(last.Value, context.Database.ProviderName!);
        var snapshot = Assert.IsType<ModelSnapshot>(assembly.ModelSnapshot, exactMatch: false);

        Assert.False(context.GetService<IMigrationsModelDiffer>().HasDifferences(
                Finalize(context, snapshot.Model).GetRelationalModel(),
                Finalize(context, Assert.IsAssignableFrom<IModel>(migration.TargetModel)).GetRelationalModel()),
            $"The model snapshot differs from the target model of {last.Key}: the snapshot changed without a migration.");
    }

    [Fact]
    public void Every_migration_class_is_discovered_through_its_designer()
    {
        using var context = new DesignTimeApplicationDbContextFactory().CreateDbContext([]);
        var discovered = context.GetService<IMigrationsAssembly>().Migrations.Values.Select(type => type.AsType()).ToHashSet();

        var migrations = typeof(ApplicationDbContext).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(Migration).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(migrations);
        Assert.All(migrations, type => Assert.True(discovered.Contains(type),
            $"{type.Name} has no [Migration] attribute (its Designer is missing): EF would skip it."));
    }

    /// <summary>Como el migrador de EF: el modelo del snapshot o de una migración se termina antes de comparar.</summary>
    private static IModel Finalize(DbContext context, IModel model)
    {
        if (model is IMutableModel mutable) model = mutable.FinalizeModel();
        return context.GetService<IModelRuntimeInitializer>().Initialize(model);
    }
}
