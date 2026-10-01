using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Detecta números de punto flotante en entidades y decimales sin precisión explícita en EF. Protege la
/// representación exacta de importes y valores decimales.
/// </summary>
public sealed class DecimalPrecisionTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Domain");

    [Fact]
    public void Domain_entities_do_not_use_binary_floating_point()
    {
        var offenders = DomainAssembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Entity)))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => IsBinaryFloatingPoint(property.PropertyType))
                .Select(property => $"{type.FullName}.{property.Name}"));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Configured_decimal_properties_have_explicit_precision()
    {
        var model = BuildConfiguredModel();

        Assert.Empty(DecimalsWithoutPrecision(model));
    }

    [Fact]
    public void Precision_guard_uses_the_application_context_model_with_conventions()
    {
        Assert.Equal(Schemas.Platform, BuildConfiguredModel().GetDefaultSchema());
    }

    [Fact]
    public void Detector_finds_missing_precision_and_binary_floating_point()
    {
        var options = new DbContextOptionsBuilder<DecimalProbeContext>()
            .UseNpgsql("Host=localhost;Database=precision_probe")
            .Options;
        using var context = new DecimalProbeContext(options);
        var model = context.Model;

        Assert.Contains(DecimalsWithoutPrecision(model), name => name.EndsWith(".Amount", StringComparison.Ordinal));
        Assert.True(IsBinaryFloatingPoint(typeof(double?)));
        Assert.True(IsBinaryFloatingPoint(typeof(float)));
        Assert.False(IsBinaryFloatingPoint(typeof(decimal)));
    }

    [Fact]
    public void Detector_finds_decimal_properties_in_owned_and_complex_types()
    {
        var options = new DbContextOptionsBuilder<DecimalProbeContext>()
            .UseNpgsql("Host=localhost;Database=precision_probe")
            .Options;
        using var context = new DecimalProbeContext(options);
        var violations = DecimalsWithoutPrecision(context.Model).ToArray();

        Assert.Contains(violations, name => name.EndsWith(".Amount", StringComparison.Ordinal));
        Assert.Contains(violations, name => name.Contains(nameof(OwnedAmount), StringComparison.Ordinal));
        Assert.Contains(violations, name => name.Contains("Complex.Amount", StringComparison.Ordinal));
    }

    private static IModel BuildConfiguredModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=appdb")
            .Options;
        using var context = new ApplicationDbContext(options, new EmptyTenantContext());
        return context.Model;
    }

    private static IEnumerable<string> DecimalsWithoutPrecision(IReadOnlyModel model) =>
        model.GetEntityTypes()
            .SelectMany(entity => DecimalPropertiesWithoutPrecision(entity, entity.ClrType.FullName ?? entity.Name));

    private static IEnumerable<string> DecimalPropertiesWithoutPrecision(IReadOnlyTypeBase type, string path) =>
        type.GetProperties()
            .Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(decimal)
                && property.GetPrecision() is null)
            .Select(property => $"{path}.{property.Name}")
            .Concat(type.GetComplexProperties()
                .SelectMany(property => DecimalPropertiesWithoutPrecision(property.ComplexType, $"{path}.{property.Name}")));

    private static bool IsBinaryFloatingPoint(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var value
        && (value == typeof(double) || value == typeof(float));

#pragma warning disable CA1812 // Only inspected by the architecture test.
    private sealed class DecimalProbe
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }

        public OwnedAmount Owned { get; set; } = new();

        public ComplexAmount Complex { get; set; } = new();
    }

    private sealed class OwnedAmount
    {
        public decimal Amount { get; set; }
    }

    private sealed class ComplexAmount
    {
        public decimal Amount { get; set; }
    }

    private sealed class DecimalProbeContext(DbContextOptions<DecimalProbeContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DecimalProbe>().OwnsOne(probe => probe.Owned);
            modelBuilder.Entity<DecimalProbe>().ComplexProperty(probe => probe.Complex);
        }
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantKind? TenantKind => null;

        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant in architecture tests.");
    }
#pragma warning restore CA1812
}
