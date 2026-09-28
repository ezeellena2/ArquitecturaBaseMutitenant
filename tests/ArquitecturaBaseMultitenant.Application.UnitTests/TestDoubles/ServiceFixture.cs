using ArquitecturaBaseMultitenant.Application.Common.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;

/// <summary>Servicios transversales reales y dobles de reloj y logger para tests de casos de uso.</summary>
internal sealed class ServiceFixture<TService> : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public ServiceFixture(params IValidator[] validators)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(TimeProvider);
        services.AddSingleton<ILogger<TService>>(Logger);
        services.AddScoped<IRequestValidator, RequestValidator>();

        foreach (var validator in validators)
        {
            var contract = validator.GetType().GetInterfaces()
                .Single(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IValidator<>));
            services.AddSingleton(contract, validator);
        }

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateScope();
        Validator = _scope.ServiceProvider.GetRequiredService<IRequestValidator>();
    }

    public FakeTimeProvider TimeProvider { get; } =
        new(new DateTimeOffset(2026, 9, 28, 15, 0, 0, TimeSpan.Zero));

    public FakeLogger<TService> Logger { get; } = new();

    public IRequestValidator Validator { get; }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
