using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using NetArchTest.Rules;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class ControllerServiceRepositoryTests
{
    private const string DomainNamespace = "ArquitecturaBaseMultitenant.Domain";
    private const string ApplicationNamespace = "ArquitecturaBaseMultitenant.Application";
    private const string ApiNamespace = "ArquitecturaBaseMultitenant.Api";

    private static readonly Assembly DomainAssembly = Assembly.Load(DomainNamespace);
    private static readonly Assembly ApplicationAssembly = Assembly.Load(ApplicationNamespace);
    private static readonly Assembly ApiAssembly = Assembly.Load(ApiNamespace);

    [Fact]
    public void Domain_does_not_define_business_persistence_contracts()
    {
        var contracts = DomainAssembly.GetTypes()
            .Where(type => type.IsInterface)
            .Where(type => type.Name.EndsWith("Repository", StringComparison.Ordinal)
                || type.Name.EndsWith("Reader", StringComparison.Ordinal))
            .Select(type => type.FullName);

        Assert.Empty(contracts);
    }

    [Fact]
    public void Api_does_not_access_entity_framework_directly()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Controllers_do_not_access_persistence_integrations_or_handlers_directly()
    {
        var controllersNamespace = ApiNamespace + ".Controllers";

        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace(controllersNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace + ".Interfaces.Persistence",
                ApplicationNamespace + ".Interfaces.Integrations",
                ApplicationNamespace + ".Abstractions.Messaging",
                ApplicationNamespace + ".Features",
                "ArquitecturaBaseMultitenant.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Every_controller_injects_an_application_service_interface()
    {
        var controllers = ApiAssembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true }
                && IsInNamespace(type, ApiNamespace + ".Controllers")
                && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(controllers.SelectMany(InvalidConstructorDependencies));
    }

    [Fact]
    public void Detector_rejects_reference_catalogs_in_controller_constructors()
    {
        Assert.Empty(InvalidConstructorDependencies(typeof(ValidController)));
        Assert.Contains(InvalidConstructorDependencies(typeof(InvalidController)),
            description => description.Contains(nameof(ICurrencyCatalog), StringComparison.Ordinal));
    }

    [Fact]
    public void Old_endpoint_and_handler_pipeline_types_are_absent()
    {
        Assert.DoesNotContain(ApiAssembly.GetTypes(), type =>
            type.Namespace?.StartsWith(ApiNamespace + ".Endpoints", StringComparison.Ordinal) == true
            || type.Name is "IEndpoint" or "EndpointExtensions");
        Assert.DoesNotContain(ApplicationAssembly.GetTypes(), type =>
            type.Namespace?.StartsWith(ApplicationNamespace + ".Features", StringComparison.Ordinal) == true
            || type.Namespace?.StartsWith(ApplicationNamespace + ".Abstractions", StringComparison.Ordinal) == true
            || type.Name.StartsWith("ICommandHandler", StringComparison.Ordinal)
            || type.Name.StartsWith("IQueryHandler", StringComparison.Ordinal));
    }

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result) =>
        Assert.True(result.IsSuccessful, "Types breaking the rule: " + string.Join(", ", result.FailingTypeNames ?? []));

    private static IEnumerable<string> InvalidConstructorDependencies(Type controller)
    {
        var constructors = controller.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (constructors.Length != 1)
        {
            return [$"{controller.FullName}: expected one constructor, found {constructors.Length}"];
        }

        var parameters = constructors[0].GetParameters();
        if (parameters.Length == 0)
        {
            return [$"{controller.FullName}: missing an application service"];
        }

        return parameters
            .Where(parameter => !parameter.ParameterType.IsInterface
                || parameter.ParameterType.Namespace != ApplicationNamespace + ".Interfaces.Services"
                || !parameter.ParameterType.Name.EndsWith("Service", StringComparison.Ordinal))
            .Select(parameter => $"{controller.FullName}: {parameter.ParameterType.FullName}");
    }

    private static bool IsInNamespace(Type type, string @namespace) =>
        type.Namespace == @namespace
        || type.Namespace?.StartsWith(@namespace + ".", StringComparison.Ordinal) == true;

#pragma warning disable CA1812, CS9113 // Signature-only test probes.
    private sealed class ValidController(IReferenceDataService service);

    private sealed class InvalidController(ICurrencyCatalog catalog);
#pragma warning restore CA1812, CS9113
}
