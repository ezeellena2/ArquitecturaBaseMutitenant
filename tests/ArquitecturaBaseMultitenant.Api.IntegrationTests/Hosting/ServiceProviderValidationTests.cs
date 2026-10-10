using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting;

/// <summary>
/// El host de pruebas valida cada registro al construirse, como Development: una clase que pide algo sin registrar
/// rompe el arranque de toda la suite, en lugar de fallar recién en el pedido que la usa.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ServiceProviderValidationTests(ApiFactory factory)
{
    [Fact]
    public void Installed_assistant_resolves_its_services_and_persistence_ports()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.NotNull(services.GetRequiredService<IAssistantConversationService>());
        Assert.NotNull(services.GetRequiredService<IAssistantSettingsService>());
        Assert.NotNull(services.GetRequiredService<IAssistantConversationReader>());
        Assert.NotNull(services.GetRequiredService<IAssistantSettingsReader>());
        Assert.NotNull(services.GetRequiredService<IAssistantConversationRepository>());
        Assert.NotNull(services.GetRequiredService<IPlatformAssistantConversationRepository>());
    }
    [Fact]
    public void The_test_host_rejects_a_registration_it_cannot_build()
    {
        using var broken = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<RegistrationWithoutItsDependency>()));

        var error = Assert.ThrowsAny<Exception>(() => broken.Services);

        Assert.Contains(nameof(RegistrationWithoutItsDependency), error.ToString(), StringComparison.Ordinal);
    }

#pragma warning disable CA1812 // Se registran solo para que el host intente construirlas.
    private sealed class UnregisteredDependency;

    private sealed class RegistrationWithoutItsDependency(UnregisteredDependency dependency)
    {
        public UnregisteredDependency Dependency { get; } = dependency;
    }
#pragma warning restore CA1812
}
