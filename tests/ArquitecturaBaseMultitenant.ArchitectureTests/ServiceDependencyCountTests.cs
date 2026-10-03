using System.Reflection;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que los constructores de servicios no superen ocho dependencias de negocio. Ayuda a detectar
/// responsabilidades que necesitan un helper propio.
/// </summary>
public sealed class ServiceDependencyCountTests
{
    private const string ServicesNamespace = "ArquitecturaBaseMultitenant.Application.Services";
    private const int MaximumDependencies = 8;

    private static readonly Assembly ApplicationAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Application");

    [Fact]
    public void Application_service_constructors_have_at_most_eight_dependencies()
    {
        var classes = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && IsInNamespace(type, ServicesNamespace)
                && !IsGenerated(type))
            .ToArray();

        Assert.NotEmpty(classes);
        Assert.Empty(classes
            .Where(type => DependencyCount(type) > MaximumDependencies)
            .Select(type => $"{type.FullName}: {DependencyCount(type)} business dependencies"));
    }

    [Fact]
    public void Detector_excludes_only_the_four_cross_cutting_dependencies()
    {
        Assert.Equal(0, DependencyCount(typeof(InfrastructureOnly)));
        Assert.Equal(8, DependencyCount(typeof(EightBusinessDependencies)));
        Assert.Equal(9, DependencyCount(typeof(NineBusinessDependencies)));
        Assert.True(DependencyCount(typeof(NineBusinessDependencies)) > MaximumDependencies);
        Assert.Equal(0, DependencyCount(typeof(NoDependencies)));
    }

    private static int DependencyCount(Type type) =>
        type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(constructor => constructor.GetParameters().Count(parameter => IsBusinessDependency(parameter.ParameterType)))
            .DefaultIfEmpty(0)
            .Max();

    private static bool IsBusinessDependency(Type type) =>
        type != typeof(TimeProvider) && type != typeof(IRequestValidator) && type != typeof(IUnitOfWork)
        && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>));

    private static bool IsGenerated(Type type) =>
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
        || (type.DeclaringType is { } declaring && IsGenerated(declaring));

    private static bool IsInNamespace(Type type, string @namespace) =>
        type.Namespace == @namespace
        || type.Namespace?.StartsWith(@namespace + ".", StringComparison.Ordinal) == true;

#pragma warning disable CA1812, CS9113 // Signature-only test probes.
    private sealed class InfrastructureOnly(ILogger<InfrastructureOnly> logger, TimeProvider clock,
        IRequestValidator validator, IUnitOfWork unitOfWork);

    private sealed class EightBusinessDependencies(string a, string b, string c, string d,
        string e, string f, string g, string h, ILogger<EightBusinessDependencies> logger,
        TimeProvider clock, IRequestValidator validator, IUnitOfWork unitOfWork);

    private sealed class NineBusinessDependencies(string a, string b, string c, string d,
        string e, string f, string g, string h, string i, ILogger<NineBusinessDependencies> logger,
        TimeProvider clock, IRequestValidator validator, IUnitOfWork unitOfWork);

    private sealed class NoDependencies;
#pragma warning restore CA1812, CS9113
}
