using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Configuration.Invitations;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.UnitTests;

/// <summary>
/// <c>AddApplication</c> registra cada clase de Services una sola vez y es dueño de las opciones de Application. Dos
/// registros de la misma clase dan dos instancias en el mismo pedido; una opción sin registrar se lee con sus valores
/// por defecto, sin avisar.
/// </summary>
public sealed class DependencyInjectionTests
{
    private const string ServicesNamespace = "ArquitecturaBaseMultitenant.Application.Services";

    [Fact]
    public void Each_services_class_is_registered_only_once()
    {
        var services = new ServiceCollection();

        services.AddApplication();
        ArquitecturaBaseMultitenant.Application.Modules.Assistant.AssistantModule.AddAssistantModule(services);
        ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.WhatsAppModule.AddWhatsAppModule(services);

        var repeated = services
            .Where(descriptor => !descriptor.IsKeyedService)
            .Select(descriptor => descriptor.ImplementationType)
            .OfType<Type>()
            .Where(type => type.Namespace?.StartsWith(ServicesNamespace, StringComparison.Ordinal) == true
                || System.Text.RegularExpressions.Regex.IsMatch(type.Namespace ?? "",
                    @"^ArquitecturaBaseMultitenant\.Application\.Modules\.[^.]+\.Services(\.|$)"))
            .GroupBy(type => type)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Name} ({group.Count()})");

        Assert.Empty(repeated);
    }

    [Theory]
    [InlineData(typeof(LoginCodeOptions))]
    [InlineData(typeof(InvitationOptions))]
    [InlineData(typeof(OutboxOptions))]
    public void Application_binds_and_validates_its_own_options(Type options)
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IOptionsChangeTokenSource<>).MakeGenericType(options));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IValidateOptions<>).MakeGenericType(options));
    }

    [Fact]
    public void Detector_rejects_duplicate_module_service_registration()
    {
        var services = new ServiceCollection();
        ArquitecturaBaseMultitenant.Application.Modules.Assistant.AssistantModule.AddAssistantModule(services);
        ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.WhatsAppModule.AddWhatsAppModule(services);
        ArquitecturaBaseMultitenant.Application.Modules.Assistant.AssistantModule.AddAssistantModule(services);
        ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.WhatsAppModule.AddWhatsAppModule(services);
        Assert.Contains(services.Where(descriptor => descriptor.ImplementationType is not null)
            .GroupBy(descriptor => descriptor.ImplementationType), group => group.Count() > 1);
    }
}
