using System.Reflection;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.BusinessAccess;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Idempotency;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Expone a MVC los controllers exclusivos de pruebas de rutas, accesos, aislamiento e idempotencia.
/// Permite ejercitar el pipeline HTTP real sin sumar esas rutas a la API de producción.
/// </summary>
internal sealed class TestControllerApplicationPart : ApplicationPart, IApplicationPartTypeProvider
{
    public override string Name => nameof(TestControllerApplicationPart);

    public IEnumerable<TypeInfo> Types =>
    [
        typeof(TestController).GetTypeInfo(),
        typeof(BusinessOnlyController).GetTypeInfo(),
        typeof(WidgetsController).GetTypeInfo(),
        typeof(IdempotencyTestController).GetTypeInfo(),
        typeof(AuthPipelineProbeController).GetTypeInfo(),
    ];
}
