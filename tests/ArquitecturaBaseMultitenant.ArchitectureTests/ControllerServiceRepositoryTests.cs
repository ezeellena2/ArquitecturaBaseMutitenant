using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Authentication;
using ArquitecturaBaseMultitenant.Api.Controllers.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
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
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .Inherit(typeof(ControllerBase))
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenControllerDependencies().ToArray())
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Every_controller_injects_an_application_service_interface()
    {
        var violations = ControllerTypes(ApiAssembly)
            .SelectMany(controller => InvalidConstructorDependencies(controller)
                .Concat(InvalidActionDependencies(controller)));

        Assert.Empty(violations);
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
        string[] missingService = parameters.Any(parameter => IsApplicationService(parameter.ParameterType))
            ? [] : new[] { $"{controller.FullName}: missing an application service" };

        return missingService.Concat(parameters
            .Where(parameter => !IsApplicationService(parameter.ParameterType)
                && !IsApprovedProtocolDependency(controller, parameter.ParameterType))
            .Select(parameter => $"{controller.FullName}: {parameter.ParameterType.FullName}"));
    }

    private static bool IsApplicationService(Type type) => type.IsInterface
        && type.Namespace == ApplicationNamespace + ".Interfaces.Services"
        && type.Name.EndsWith("Service", StringComparison.Ordinal);

    private static bool IsApprovedProtocolDependency(Type controller, Type dependency) =>
        controller == typeof(ConnectController) && dependency == typeof(OpenIdPrincipalFactory)
        || controller == typeof(ExternalLoginController) && dependency == typeof(IAuthenticationSchemeProvider);

    [Fact]
    public void Detector_recognizes_controllerbase_subclasses_without_controller_suffix()
    {
        Assert.Contains(ControllerTypes(typeof(ControllerServiceRepositoryTests).Assembly),
            type => type == typeof(UnconventionallyNamedEndpoint));
    }

    [Fact]
    public void Detector_rejects_reference_catalogs_in_action_parameters()
    {
        Assert.Contains(InvalidActionDependencies(typeof(UnconventionallyNamedEndpoint)),
            description => description.Contains(nameof(ICurrencyCatalog), StringComparison.Ordinal));
    }

    [Fact]
    public void Detector_rejects_reference_catalog_dependencies()
    {
        Assert.Contains(ForbiddenControllerDependencies(),
            dependency => dependency == ApplicationNamespace + ".Interfaces.ReferenceData");
    }

    private static IEnumerable<Type> ControllerTypes(Assembly assembly) =>
        assembly.GetTypes().Where(type => type is { IsAbstract: false, IsClass: true }
            && type.IsSubclassOf(typeof(ControllerBase)));

    private static IEnumerable<string> InvalidActionDependencies(Type controller) =>
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters()
                .Where(parameter => IsForbiddenActionParameter(parameter.ParameterType))
                .Select(parameter => $"{controller.FullName}.{method.Name}: {parameter.ParameterType.FullName}"));

    private static bool IsForbiddenActionParameter(Type type) =>
        type.Namespace?.StartsWith(ApplicationNamespace + ".Interfaces", StringComparison.Ordinal) == true
        || type.Namespace?.StartsWith("ArquitecturaBaseMultitenant.Infrastructure", StringComparison.Ordinal) == true
        || type.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true;

    private static IEnumerable<string> ForbiddenControllerDependencies() =>
    [
        ApplicationNamespace + ".Interfaces.Persistence",
        ApplicationNamespace + ".Interfaces.Integrations",
        ApplicationNamespace + ".Interfaces.ReferenceData",
        ApplicationNamespace + ".Abstractions.Messaging",
        ApplicationNamespace + ".Features",
        "ArquitecturaBaseMultitenant.Infrastructure",
        "Microsoft.EntityFrameworkCore",
    ];

#pragma warning disable CA1812, CS9113 // Signature-only test probes.
    private sealed class ValidController(IReferenceDataService service);

    private sealed class InvalidController(ICurrencyCatalog catalog);

    private sealed class UnconventionallyNamedEndpoint(IReferenceDataService service) : ControllerBase
    {
        [HttpGet]
        public OkObjectResult Get(ICurrencyCatalog catalog) => Ok(catalog);
    }
#pragma warning restore CA1812, CS9113
}
