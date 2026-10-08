using ArquitecturaBaseMultitenant.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class PersistenceModelComparisonTests
{
    [Theory]
    [InlineData(typeof(MissingIndexContext))]
    [InlineData(typeof(MissingForeignKeyContext))]
    [InlineData(typeof(MissingConcurrencyContext))]
    [InlineData(typeof(MissingFilterContext))]
    [InlineData(typeof(ChangedNullabilityContext))]
    public void Detector_rejects_index_foreign_key_xmin_filter_and_nullability_changes(Type changedType)
    {
        using var source = new ControlContext();
        using var changed = (ControlContext)Activator.CreateInstance(changedType, nonPublic: true)!;
        Assert.True(PersistenceModelComparison.HasDifferences(source, changed));
    }

    [Fact]
    public void Identical_models_are_equal()
    {
        using var source = new ControlContext();
        using var target = new ControlContext();
        PersistenceModelComparison.AssertEqual(source, target);
    }

    private class ControlContext : DbContext
    {
        protected virtual string Change => "";
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseNpgsql("Host=localhost;Database=model-comparison-control");
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Parent>().HasKey(value => value.Id);
            var child = builder.Entity<Child>();
            child.HasKey(value => value.Id);
            child.Property(value => value.Name).IsRequired(Change != "nullability");
            child.Property<uint>("Version").HasColumnName("xmin").HasColumnType("xid")
                .IsRowVersion().IsConcurrencyToken(Change != "xmin");
            if (Change != "index") child.HasIndex(value => value.Name).IsUnique();
            if (Change != "fk") child.HasOne<Parent>().WithMany().HasForeignKey(value => value.ParentId);
            if (Change != "filter") child.HasQueryFilter("Named", value => value.Name != "");
        }
    }
    private sealed class MissingIndexContext : ControlContext { protected override string Change => "index"; }
    private sealed class MissingForeignKeyContext : ControlContext { protected override string Change => "fk"; }
    private sealed class MissingConcurrencyContext : ControlContext { protected override string Change => "xmin"; }
    private sealed class MissingFilterContext : ControlContext { protected override string Change => "filter"; }
    private sealed class ChangedNullabilityContext : ControlContext { protected override string Change => "nullability"; }
    private sealed class Parent { public Guid Id { get; set; } }
    private sealed class Child { public Guid Id { get; set; } public Guid ParentId { get; set; } public string? Name { get; set; } }
}
