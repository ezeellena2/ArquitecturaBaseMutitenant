using System.Reflection;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

internal sealed class TestControllerApplicationPart : ApplicationPart, IApplicationPartTypeProvider
{
    public override string Name => nameof(TestControllerApplicationPart);

    public IEnumerable<TypeInfo> Types => [typeof(TestController).GetTypeInfo()];
}
