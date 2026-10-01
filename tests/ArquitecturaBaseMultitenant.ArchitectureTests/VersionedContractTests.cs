using System.Reflection;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que las ediciones de entidades con versión reciban esa versión en su contrato HTTP. Protege la
/// detección de cambios simultáneos.
/// </summary>
public sealed class VersionedContractTests
{
    [Fact]
    public void Versioned_entity_edits_expose_a_version_in_the_http_contract()
    {
        var versionedEntityNames = typeof(IVersioned).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IVersioned).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        var api = Assembly.Load("ArquitecturaBaseMultitenant.Api");
        var offenders = api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(typeof(HttpPutAttribute)) || method.IsDefined(typeof(HttpDeleteAttribute)))
            .SelectMany(method => method.GetParameters()
                .Where(parameter => parameter.ParameterType.Namespace?.StartsWith(
                    "ArquitecturaBaseMultitenant.Api.Contracts", StringComparison.Ordinal) == true)
                .Select(parameter => (Method: method, Contract: parameter.ParameterType)))
            .Where(item => versionedEntityNames.Any(name => item.Contract.Name.Contains(name, StringComparison.Ordinal)))
            .Where(item => !HasVersion(item.Contract))
            .Select(item => $"{item.Method.DeclaringType?.Name}.{item.Method.Name}: {item.Contract.Name}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Guard_detects_a_missing_version_and_accepts_a_versioned_contract()
    {
        Assert.False(HasVersion(typeof(WithoutVersion)));
        Assert.True(HasVersion(typeof(WithVersion)));
    }

    private static bool HasVersion(Type contract) =>
        contract.GetProperty("Version") is { PropertyType: var propertyType }
        && (propertyType == typeof(uint) || propertyType == typeof(uint?));

    private sealed record WithoutVersion(string Name);

    private sealed record WithVersion(string Name, uint Version);
}
