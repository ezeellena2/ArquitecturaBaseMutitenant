using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Features;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Si una organización tiene prendido el módulo, para los trabajos sin sesión (la cola de entrada, los correos, las
/// difusiones): lee sus habilitaciones con su vencimiento, porque <c>IFeatureService</c> responde por el acceso de una
/// sesión. Con una fila por origen, vale la regla de <see cref="TenantFeatureRow.IsModuleEnabled"/> con el valor de entrada
/// del catálogo.
/// </summary>
internal sealed class WhatsAppModulePolicy(ITenantFeatureCache features, IFeatureCatalog catalog, ITenantContext tenant, TimeProvider clock)
{
    internal Task<bool> IsCurrentEnabledAsync(CancellationToken ct) => IsEnabledAsync(tenant.RequiredTenantId, ct);

    internal async Task<bool> IsEnabledAsync(Guid organizationId, CancellationToken ct)
    {
        var definition = catalog.All.SingleOrDefault(row => row.Key == WhatsAppFeature.Key);
        if (definition is null) return false;
        var rows = await features.ListAsync(organizationId, ct);
        return TenantFeatureRow.IsModuleEnabled(rows, definition.Key, definition.DefaultEnabled, clock.GetUtcNow().UtcDateTime);
    }
}
