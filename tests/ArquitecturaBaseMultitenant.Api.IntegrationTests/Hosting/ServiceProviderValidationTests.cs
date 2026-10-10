using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting;

/// <summary>
/// El host de pruebas valida cada registro al construirse, como Development: una clase que pide algo sin registrar
/// rompe el arranque de toda la suite, en lugar de fallar recién en el pedido que la usa.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed partial class ServiceProviderValidationTests(ApiFactory factory)
{
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
