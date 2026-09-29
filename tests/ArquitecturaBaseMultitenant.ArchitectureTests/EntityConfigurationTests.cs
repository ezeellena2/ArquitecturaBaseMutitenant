using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class EntityConfigurationTests
{
    private const string ConfigurationsNamespace =
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations";

    [Fact]
    public void Every_mapped_entity_has_exactly_one_configuration()
    {
        // Se mira el modelo productivo y también las entidades esperadas: si se quita una configuración,
        // ApplyConfigurationsFromAssembly deja de mapearla y la primera lista sola no lo detectaría.
        var mapped = ArchitectureModel.EntityTypes()
            .Where(entity => entity.BaseType is null && !entity.IsOwned())
            .Select(entity => entity.ClrType)
            .ToArray();
        var infrastructure = Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure");
        var configured = infrastructure.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.Namespace?.StartsWith(ConfigurationsNamespace + ".", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                .Select(contract => contract.GenericTypeArguments[0]))
            .ToArray();
        var deferred = new[]
        {
            typeof(ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant),
            typeof(Member),
        };
        var domainEntities = typeof(Entity).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && (type.IsSubclassOf(typeof(Entity))
                    || type.Namespace == typeof(Currency).Namespace))
            .Where(type => HarnessStage.Closed >= 3 || !deferred.Contains(type));
        var expected = domainEntities.Concat(
        [
            typeof(DataProtectionKey),
            infrastructure.GetType("ArquitecturaBaseMultitenant.Infrastructure.Idempotency.IdempotencyKey",
                throwOnError: true)!,
        ]).ToArray();

        Assert.NotEmpty(mapped);
        Assert.Empty(Missing(expected, mapped).Select(type => $"Unmapped entity: {type.FullName}"));
        Assert.Empty(Missing(mapped, configured).Select(type => type.FullName));
        Assert.Empty(configured.Except(mapped).Select(type => $"Unmapped configuration: {type.FullName}"));
        Assert.Empty(configured.GroupBy(type => type)
            .Where(group => group.Count() != 1)
            .Select(group => $"Duplicate configuration: {group.Key.FullName}"));
    }

    [Fact]
    public void Detector_rejects_an_entity_without_configuration()
    {
        Assert.Equal([typeof(UnconfiguredEntity)],
            Missing([typeof(UnconfiguredEntity)], [typeof(string)]));
        Assert.Empty(Missing([typeof(UnconfiguredEntity)], [typeof(UnconfiguredEntity)]));
    }

    private static Type[] Missing(IEnumerable<Type> mapped, IEnumerable<Type> configured) =>
        [.. mapped.Except(configured)];

#pragma warning disable CA1812 // Caso de control de la regla, solo se inspecciona.
    private sealed class UnconfiguredEntity;
#pragma warning restore CA1812
}

internal static class ArchitectureModel
{
    public static IReadOnlyList<IEntityType> EntityTypes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_guard")
            .Options;
        using var context = new ApplicationDbContext(options, new NoTenantContext());
        return [.. context.Model.GetEntityTypes()];
    }

    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantKind? TenantKind => null;

        public Guid RequiredTenantId => throw new NotSupportedException();
    }
}
