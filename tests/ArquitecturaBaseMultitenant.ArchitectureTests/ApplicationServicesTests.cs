using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que cada servicio de Application implemente su interfaz de servicio, y que cada helper termine con un
/// sufijo de la lista cerrada de <c>capas-y-flujo.md</c>. Mantiene explícito el contrato que consumen los controllers
/// y el vocabulario de las piezas de un área.
/// </summary>
public sealed partial class ApplicationServicesTests
{
    private const string ServicesNamespace = "ArquitecturaBaseMultitenant.Application.Services";
    private const string ServiceInterfacesNamespace = "ArquitecturaBaseMultitenant.Application.Interfaces.Services";
    private const string SuffixesHeading = "**Sufijos de los helpers**";

    private static readonly Assembly ApplicationAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Application");

    [Fact]
    public void Every_application_service_implements_a_service_interface()
    {
        // Los controllers inyectan la interfaz, nunca la clase: un *Service sin su contrato en Interfaces.Services no se
        // puede usar desde la Api sin romper ControllerServiceRepositoryTests. Las piezas internas de un área que no
        // terminan en Service (UserGuard, LoginCodeIssuer, PhoneNumberLinker) no entran en esta regla.
        var services = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && ApplicationNamespaceShape.IsServices(type)
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

    [Fact]
    public void Every_helper_ends_with_a_suffix_of_the_closed_list()
    {
        var suffixes = ClosedHelperSuffixes();
        Assert.NotEmpty(suffixes);

        var outside = Helpers()
            .Where(helper => !HasClosedSuffix(helper, suffixes))
            .Select(helper => helper.Name);

        Assert.Empty(outside);
    }

    [Fact]
    public void Every_suffix_of_the_closed_list_is_in_use()
    {
        // La lista es la de los helpers que existen: un sufijo que ya nadie usa sale de la tabla.
        var helpers = Helpers();

        var unused = ClosedHelperSuffixes()
            .Where(suffix => !helpers.Any(helper => helper.Name.EndsWith(suffix, StringComparison.Ordinal)));

        Assert.Empty(unused);
    }

    [Fact]
    public void Detector_rejects_a_helper_whose_suffix_is_not_in_the_list() =>
        Assert.False(HasClosedSuffix(typeof(PaymentCoordinator), ["Guard", "Policy"]));

    [Fact]
    public void Neutral_integration_implementations_are_adapters_and_helpers_are_not()
    {
        Assert.True(ApplicationNamespaceShape.IsIntegrationAdapter(typeof(
            ArquitecturaBaseMultitenant.Application.Modules.Control.Services.ControlPhoneObserver)));
        Assert.False(ApplicationNamespaceShape.IsIntegrationAdapter(typeof(
            ArquitecturaBaseMultitenant.Application.Modules.Control.Services.InvalidHelperGuard)));
    }

    /// <summary>
    /// Un helper es una clase de Services que arma el contenedor y no termina en Service. Así no entran los modelos
    /// internos (<c>IssuedLoginCode</c>), los records, las clases estáticas ni los <c>*Registration</c>.
    /// </summary>
    static partial void AddAssistantServices(IServiceCollection services);
    static partial void AddWhatsAppServices(IServiceCollection services);

    private static Type[] Helpers()
    {
        var registrations = new ServiceCollection();
        registrations.AddApplication();
        AddAssistantServices(registrations);
        AddWhatsAppServices(registrations);

        return
        [
            .. registrations
                .Where(descriptor => !descriptor.IsKeyedService)
                .Select(descriptor => descriptor.ImplementationType)
                .OfType<Type>()
                .Where(type => ApplicationNamespaceShape.IsServices(type)
                    && !type.Name.EndsWith("Service", StringComparison.Ordinal)
                    && !ApplicationNamespaceShape.IsIntegrationAdapter(type))
                .Distinct(),
        ];
    }

    /// <summary>La tabla de capas-y-flujo.md que sigue a «Sufijos de los helpers»: una fila por sufijo.</summary>
    private static string[] ClosedHelperSuffixes()
    {
        var lines = File.ReadAllLines(Path.Combine(SolutionRoot.FullPath, "docs", "rules", "capas-y-flujo.md"));
        var start = Array.FindIndex(lines, line => line.StartsWith(SuffixesHeading, StringComparison.Ordinal));
        Assert.True(start >= 0, $"capas-y-flujo.md no tiene la lista {SuffixesHeading}.");

        return
        [
            .. lines.Skip(start + 1)
                .SkipWhile(line => !line.StartsWith('|'))
                .TakeWhile(line => line.StartsWith('|'))
                .Select(line => SuffixRow().Match(line))
                .Where(match => match.Success)
                .Select(match => match.Groups["suffix"].Value),
        ];
    }

    private static bool HasClosedSuffix(Type helper, IEnumerable<string> suffixes) =>
        suffixes.Any(suffix => helper.Name.EndsWith(suffix, StringComparison.Ordinal));

    private static bool HasServiceContract(Type service) =>
        service.GetInterfaces().Any(ApplicationNamespaceShape.IsServiceContract);

    private static bool IsInNamespace(Type type, string @namespace) =>
        type.Namespace == @namespace
        || type.Namespace?.StartsWith(@namespace + ".", StringComparison.Ordinal) == true;

    [GeneratedRegex(@"^\|\s*`(?<suffix>[A-Za-z]+)`\s*\|", RegexOptions.CultureInvariant)]
    private static partial Regex SuffixRow();

#pragma warning disable CA1812 // Signature-only test probes.
    private sealed class UncontractedService;

    private sealed class PaymentCoordinator;
#pragma warning restore CA1812
}
