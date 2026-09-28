using System.Reflection;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class DecimalPrecisionTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Domain");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure");

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
    public void Detector_finds_missing_precision_and_binary_floating_point()
    {
        var builder = new ModelBuilder(new ConventionSet());
        builder.Entity<DecimalProbe>().Property(entity => entity.Amount);

        Assert.Contains(DecimalsWithoutPrecision(builder.Model), name => name.EndsWith(".Amount", StringComparison.Ordinal));
        Assert.True(IsBinaryFloatingPoint(typeof(double?)));
        Assert.True(IsBinaryFloatingPoint(typeof(float)));
        Assert.False(IsBinaryFloatingPoint(typeof(decimal)));

        builder.Entity<DecimalProbe>().Property(entity => entity.Amount).HasPrecision(19, 4);
        Assert.Empty(DecimalsWithoutPrecision(builder.Model));
    }

    private static IMutableModel BuildConfiguredModel()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        var apply = typeof(ModelBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == nameof(ModelBuilder.ApplyConfiguration)
                && method.IsGenericMethodDefinition);

        foreach (var configurationType in InfrastructureAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }))
        {
            foreach (var contract in configurationType.GetInterfaces()
                .Where(type => type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            {
                var configuration = Activator.CreateInstance(configurationType, nonPublic: true)
                    ?? throw new InvalidOperationException($"Cannot construct {configurationType.FullName}.");
                apply.MakeGenericMethod(contract.GenericTypeArguments[0])
                    .Invoke(modelBuilder, [configuration]);
            }
        }

        return modelBuilder.Model;
    }

    private static IEnumerable<string> DecimalsWithoutPrecision(IMutableModel model) =>
        model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()
                .Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(decimal)
                    && property.GetPrecision() is null)
                .Select(property => $"{entity.ClrType.FullName}.{property.Name}"));

    private static bool IsBinaryFloatingPoint(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var value
        && (value == typeof(double) || value == typeof(float));

#pragma warning disable CA1812 // Only inspected by the architecture test.
    private sealed class DecimalProbe
    {
        public decimal Amount { get; set; }
    }
#pragma warning restore CA1812
}
