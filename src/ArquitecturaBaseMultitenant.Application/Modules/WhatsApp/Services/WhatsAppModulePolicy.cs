using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Si una organización tiene prendido el módulo, para los trabajos sin sesión (la cola de entrada, los correos): lee la
/// habilitación de Plataforma con su vencimiento, porque <c>IFeatureService</c> responde por el acceso de una sesión.
/// </summary>
internal sealed class WhatsAppModulePolicy(ITenantFeatureCache features, TimeProvider clock)
{
    internal async Task<bool> IsEnabledAsync(Guid organizationId, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        return (await features.ListAsync(organizationId, ct)).Any(row => row.Key == WhatsAppFeature.Key && row.IsEnabledAt(nowUtc));
    }
}
