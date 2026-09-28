using System.Reflection;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class EmailPropertyTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(Email).Assembly,
        Assembly.Load("ArquitecturaBaseMultitenant.Application"),
        Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure")
    ];

    [Fact]
    public void Entities_and_application_models_do_not_expose_bare_email_strings()
    {
        var violations = Assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(IsEntityOrModel)
            .SelectMany(type => BareEmailProperties(type)
                .Select(property => $"{type.FullName}.{property.Name}"));

        Assert.Empty(violations);
    }

    [Fact]
    public void The_rule_detects_a_string_email_and_accepts_the_value_object()
    {
        Assert.True(IsBareEmailProperty(typeof(BareEmailFixture).GetProperty(nameof(BareEmailFixture.ContactEmail))!));
        Assert.False(IsBareEmailProperty(typeof(TypedEmailFixture).GetProperty(nameof(TypedEmailFixture.ContactEmail))!));
    }

    [Fact]
    public void The_rule_detects_inherited_bare_email_properties()
    {
        Assert.Contains(BareEmailProperties(typeof(InheritedEmailFixture)),
            property => property.Name == nameof(BareEmailFixture.ContactEmail));
    }

    private static IEnumerable<PropertyInfo> BareEmailProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(IsBareEmailProperty)
            .Where(property => !IsIdentityEmailCopy(type, property));

    private static bool IsEntityOrModel(Type type)
    {
        var fullName = type.FullName ?? string.Empty;
        return fullName.StartsWith("ArquitecturaBaseMultitenant.Domain.", StringComparison.Ordinal)
            || fullName.StartsWith("ArquitecturaBaseMultitenant.Application.Models.", StringComparison.Ordinal)
            || fullName.StartsWith("ArquitecturaBaseMultitenant.Infrastructure.Identity.", StringComparison.Ordinal);
    }

    private static bool IsBareEmailProperty(PropertyInfo property) =>
        property.PropertyType == typeof(string)
        && property.Name.EndsWith("Email", StringComparison.Ordinal);

    private static bool IsIdentityEmailCopy(Type type, PropertyInfo property) =>
        type.FullName == "ArquitecturaBaseMultitenant.Infrastructure.Identity.ApplicationUser"
        && property.DeclaringType?.Namespace == "Microsoft.AspNetCore.Identity"
        && property.Name is "Email" or "NormalizedEmail";

    private sealed class BareEmailFixture
    {
        public string ContactEmail { get; } = string.Empty;
    }

    private sealed class TypedEmailFixture
    {
        public Email? ContactEmail { get; }
    }

    private class InheritedEmailBaseFixture
    {
        public string ContactEmail { get; } = string.Empty;
    }

    private sealed class InheritedEmailFixture : InheritedEmailBaseFixture;
}
