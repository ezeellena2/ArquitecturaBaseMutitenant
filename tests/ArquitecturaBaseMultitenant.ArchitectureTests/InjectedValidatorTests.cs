using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.ArchitectureTests.Support;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Detecta servicios que construyen o inyectan validadores concretos en vez del puerto común. Mantiene una
/// sola entrada para la validación de solicitudes. La única excepción es una fuente de acciones del asistente
/// (<see cref="IAssistantActionSource"/>, ADR 0068 §5): recibe los <c>IValidator&lt;T&gt;</c> de su pedido y los corre con
/// <c>RequestValidator.ValidateAsync</c>, porque el puerto los resuelve con <see cref="IServiceProvider"/> y el árbol de solo
/// lectura de <c>AssistantActionDeclarationTests</c> no puede recorrerlo; así recorre cada validador y el resultado es el de la ruta.
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
                && IsInNamespace(type, ServicesNamespace)
                && !typeof(IAssistantActionSource).IsAssignableFrom(type))
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
        Assert.Contains(InjectedValidatorViolations(typeof(ProbeEnumerableService)),
            violation => violation.Contains(nameof(IValidator<string>), StringComparison.Ordinal));
        Assert.Contains(ConstructedValidatorViolations(typeof(InjectedValidatorTests).Assembly),
            violation => violation.Contains(nameof(ProbeValidator), StringComparison.Ordinal));
    }

    private static IEnumerable<string> InjectedValidatorViolations(Type service) =>
        service.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters()
                .Where(parameter => typeof(IValidator).IsAssignableFrom(Unwrapped(parameter.ParameterType)))
                .Select(parameter => $"{service.FullName}: {parameter.ParameterType.FullName}"));

    // Una lista de validadores también es inyectarlos.
    private static Type Unwrapped(Type type) =>
        type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? type.GetGenericArguments()[0] : type;

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

    private sealed class ProbeEnumerableService(IEnumerable<IValidator<string>> validators);

    private sealed class ProbeValidator : AbstractValidator<string>;
#pragma warning restore CA1812, CS9113
}
