using System.Reflection;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

internal sealed partial class TestControllerApplicationPart
{
    static partial void AddWhatsAppControllers(List<TypeInfo> types)
    {
        types.Add(typeof(WhatsAppErrorProbeController).GetTypeInfo());
        types.Add(typeof(WhatsAppQuotaProbeController).GetTypeInfo());
    }
}
