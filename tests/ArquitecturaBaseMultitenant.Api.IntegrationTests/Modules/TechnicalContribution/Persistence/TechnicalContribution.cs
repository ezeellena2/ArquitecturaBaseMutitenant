using System.Collections.Concurrent;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.TechnicalContribution.Persistence;

internal sealed class TechnicalRecord { public Guid Id { get; set; } }
internal sealed class TechnicalRecordConfiguration : IEntityTypeConfiguration<TechnicalRecord>
{
    public void Configure(EntityTypeBuilder<TechnicalRecord> builder)
    {
        builder.ToTable("TechnicalRecords", "identity", table => table.ExcludeFromMigrations()); builder.HasKey(value => value.Id);
    }
}
/// <summary>Contribución de prueba sin tablas físicas, producto, rutas ni capacidades.</summary>
internal sealed class TechnicalModelContributor : IPersistenceModelContributor
{
    public string ConfigurationNamespace => GetType().Namespace!;
    public IReadOnlyDictionary<Type, string> GlobalEntities { get; } = new Dictionary<Type, string> { [typeof(TechnicalRecord)] = "identity" };
    internal ConcurrentDictionary<IMutableModel, int> Applications { get; } = new(ReferenceEqualityComparer.Instance);
    public void Configure(ModelBuilder builder)
    {
        Applications.AddOrUpdate(builder.Model, 1, (_, value) => value + 1);
        builder.ApplyConfigurationsFromAssembly(GetType().Assembly, type => type.Namespace == ConfigurationNamespace && type == typeof(TechnicalRecordConfiguration));
    }
}
internal sealed class CountedContribution(IPersistenceModelContributor inner) : IPersistenceModelContributor
{
    public string ConfigurationNamespace => inner.ConfigurationNamespace;
    public IReadOnlyDictionary<Type, string> GlobalEntities => inner.GlobalEntities;
    internal ConcurrentDictionary<IMutableModel, int> Applications { get; } = new(ReferenceEqualityComparer.Instance);
    public void Configure(ModelBuilder builder)
    { Applications.AddOrUpdate(builder.Model, 1, (_, value) => value + 1); inner.Configure(builder); }
}
