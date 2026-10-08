using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OpenIddict.EntityFrameworkCore.Models;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que cada entidad del modelo EF tenga exactamente una configuración. Evita que un mapeo quede
/// implícito o se duplique.
/// </summary>
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
        var contributedNamespaces = PersistenceModelContributors.Discover().Select(value => value.ConfigurationNamespace).ToArray();
        var configured = infrastructure.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && (type.Namespace?.StartsWith(ConfigurationsNamespace + ".", StringComparison.Ordinal) == true
                    || contributedNamespaces.Contains(type.Namespace)))
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                .Select(contract => contract.GenericTypeArguments[0]))
            .Concat(FrameworkConfiguredEntities())
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
            .Where(type => HarnessStage.ClosedStages.Contains(3) || !deferred.Contains(type));
        var expected = domainEntities.Concat(
        [
            typeof(DataProtectionKey),
            infrastructure.GetType("ArquitecturaBaseMultitenant.Infrastructure.Identity.ApplicationUser",
                throwOnError: true)!,
            typeof(IdentityUserClaim<Guid>),
            typeof(IdentityUserLogin<Guid>),
            typeof(IdentityUserToken<Guid>),
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

    [Fact]
    public void Detector_rejects_duplicate_configurations() =>
        Assert.Single(new[] { typeof(UnconfiguredEntity), typeof(UnconfiguredEntity) }
            .GroupBy(type => type), group => group.Count() != 1);

    private static Type[] Missing(IEnumerable<Type> mapped, IEnumerable<Type> configured) =>
        [.. mapped.Except(configured)];

    private static IEnumerable<Type> FrameworkConfiguredEntities()
    {
        var arguments = new Dictionary<string, Type>
        {
            ["TKey"] = typeof(Guid),
            ["TApplication"] = typeof(OpenIddictEntityFrameworkCoreApplication<Guid>),
            ["TAuthorization"] = typeof(OpenIddictEntityFrameworkCoreAuthorization<Guid>),
            ["TScope"] = typeof(OpenIddictEntityFrameworkCoreScope<Guid>),
            ["TToken"] = typeof(OpenIddictEntityFrameworkCoreToken<Guid>),
        };
        return Assembly.Load("OpenIddict.EntityFrameworkCore").GetTypes()
            .Where(type => type.IsGenericTypeDefinition && type.GetInterfaces().Any(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .Select(type => type.MakeGenericType(type.GetGenericArguments().Select(argument => arguments[argument.Name]).ToArray()))
            .SelectMany(type => type.GetInterfaces().Where(contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .Select(contract => contract.GenericTypeArguments[0]);
    }

    [Fact]
    public void Architecture_and_design_time_have_the_same_complete_model()
    {
        using var designTime = new DesignTimeApplicationDbContextFactory().CreateDbContext([]);
        static string[] Describe(IEnumerable<IEntityType> entities) => [.. entities
            .Select(value => value.ClrType.FullName + ":" + value.GetSchema() + "." + value.GetTableName()
                + ":" + string.Join(",", value.GetProperties().Select(property => property.Name + "=" + property.GetColumnType()).Order()))
            .Order(StringComparer.Ordinal)];
        Assert.Equal(Describe(designTime.Model.GetEntityTypes()), Describe(ArchitectureModel.EntityTypes()));
    }

#pragma warning disable CA1812 // Caso de control de la regla, solo se inspecciona.
    private sealed class UnconfiguredEntity;
#pragma warning restore CA1812
}

internal static class ArchitectureModel
{
    public static IReadOnlyList<IEntityType> EntityTypes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_guard");
        options.UseOpenIddict<Guid>();
        using var context = new ApplicationDbContext(options.Options, new NoTenantContext());
        return [.. context.Model.GetEntityTypes()];
    }

    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantKind? TenantKind => null;

        public Guid RequiredTenantId => throw new NotSupportedException();
    }
}
