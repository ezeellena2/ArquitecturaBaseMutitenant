using System.Reflection;
using ArquitecturaBaseMultitenant.ArchitectureTests.Support;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Detecta servicios que construyen o inyectan validadores concretos en vez del puerto común. Mantiene una
/// sola entrada para la validación de solicitudes.
/// </summary>
public sealed class InjectedValidatorTests
{
    private const string ServicesNamespace = "ArquitecturaBaseMultitenant.Application.Services";
    private static readonly Assembly ApplicationAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Application");

    [Fact]
    public void Application_services_use_the_request_validator_port()
    {
        var constructorViolations = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && IsInNamespace(type, ServicesNamespace))
            .SelectMany(InjectedValidatorViolations);
        var constructionViolations = ConstructedValidatorViolations(ApplicationAssembly)
            .Where(violation => violation.StartsWith(ServicesNamespace + ".", StringComparison.Ordinal));

        Assert.Empty(constructorViolations.Concat(constructionViolations));
    }

    [Fact]
    public void Detector_catches_direct_validator_injection_and_construction()
    {
        Assert.IsType<ProbeValidator>(ConstructValidator());
        Assert.Contains(InjectedValidatorViolations(typeof(ProbeService)),
            violation => violation.Contains(nameof(IValidator<string>), StringComparison.Ordinal));
        Assert.Contains(ConstructedValidatorViolations(typeof(InjectedValidatorTests).Assembly),
            violation => violation.Contains(nameof(ProbeValidator), StringComparison.Ordinal));
    }

    private static IEnumerable<string> InjectedValidatorViolations(Type service) =>
        service.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters()
                .Where(parameter => typeof(IValidator).IsAssignableFrom(parameter.ParameterType))
                .Select(parameter => $"{service.FullName}: {parameter.ParameterType.FullName}"));

    private static IEnumerable<string> ConstructedValidatorViolations(Assembly assembly) =>
        CallSites.Calls(assembly)
            .Where(call => call.Method == ".ctor"
                && (call.DeclaringType.EndsWith("Validator", StringComparison.Ordinal)
                    || call.DeclaringType.Contains("Validator`", StringComparison.Ordinal)))
            .Select(call => $"{call.Owner}: new {call.DeclaringType}");

    private static bool IsInNamespace(Type type, string @namespace) =>
        type.Namespace == @namespace
        || type.Namespace?.StartsWith(@namespace + ".", StringComparison.Ordinal) == true;

    private static ProbeValidator ConstructValidator() => new();

#pragma warning disable CA1812, CS9113 // Test probes inspected by reflection and IL.
    private sealed class ProbeService(IValidator<string> validator);

    private sealed class ProbeValidator : AbstractValidator<string>;
#pragma warning restore CA1812, CS9113
}
