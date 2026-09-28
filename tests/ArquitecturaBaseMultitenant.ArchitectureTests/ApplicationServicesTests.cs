using System.Reflection;
using System.Runtime.CompilerServices;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class ApplicationServicesTests
{
    private const string ServicesNamespace = "ArquitecturaBaseMultitenant.Application.Services";
    private const string ServiceInterfacesNamespace = "ArquitecturaBaseMultitenant.Application.Interfaces.Services";

    private static readonly Assembly ApplicationAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Application");

    [Fact]
    public void Every_application_service_implements_a_service_interface()
    {
        // Los controllers inyectan la interfaz, nunca la clase: un *Service sin su contrato en Interfaces.Services no se
        // puede usar desde la Api sin romper ControllerServiceRepositoryTests. Las piezas internas de un área que no
        // terminan en Service (UserGuard, LoginCodeIssuer, PhoneNumberLinker) no entran en esta regla.
        var services = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && IsInNamespace(type, ServicesNamespace)
                && type.Name.EndsWith("Service", StringComparison.Ordinal)
                && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .ToArray();

        Assert.NotEmpty(services);

        var withoutContract = services
            .Where(service => !HasServiceContract(service))
            .Select(service => service.FullName);

        Assert.Empty(withoutContract);
    }

    [Fact]
    public void Detector_rejects_a_service_without_its_interface() =>
        Assert.False(HasServiceContract(typeof(UncontractedService)));

    private static bool HasServiceContract(Type service) =>
        service.GetInterfaces().Any(contract => IsInNamespace(contract, ServiceInterfacesNamespace));

    private static bool IsInNamespace(Type type, string @namespace) =>
        type.Namespace == @namespace
        || type.Namespace?.StartsWith(@namespace + ".", StringComparison.Ordinal) == true;

#pragma warning disable CA1812 // Signature-only test probe.
    private sealed class UncontractedService;
#pragma warning restore CA1812
}
