using System.Reflection;
using System.Runtime.CompilerServices;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

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
            .Select(type => $"{type.FullName}: {DependencyCount(type)} dependencies"));
    }

    [Fact]
    public void Detector_counts_the_whole_constructor_including_logger_and_clock()
    {
        Assert.Equal(9, DependencyCount(typeof(NineDependencies)));
        Assert.Equal(0, DependencyCount(typeof(NoDependencies)));
    }

    private static int DependencyCount(Type type) =>
        type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(constructor => constructor.GetParameters().Length)
            .DefaultIfEmpty(0)
            .Max();

    private static bool IsGenerated(Type type) =>
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
        || (type.DeclaringType is { } declaring && IsGenerated(declaring));

    private static bool IsInNamespace(Type type, string @namespace) =>
        type.Namespace == @namespace
        || type.Namespace?.StartsWith(@namespace + ".", StringComparison.Ordinal) == true;

#pragma warning disable CA1812, CS9113 // Signature-only test probes.
    private sealed class NineDependencies(
        string a, string b, string c, string d, string e, string f, string g,
        TimeProvider clock, Microsoft.Extensions.Logging.ILogger logger);

    private sealed class NoDependencies;
#pragma warning restore CA1812, CS9113
}
