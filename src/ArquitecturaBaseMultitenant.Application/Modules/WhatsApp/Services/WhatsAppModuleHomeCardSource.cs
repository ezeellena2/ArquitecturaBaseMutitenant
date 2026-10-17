using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>«WhatsApp · N números» en el Inicio, para quien tiene algún permiso de WhatsApp con el módulo prendido.</summary>
internal sealed class WhatsAppModuleHomeCardSource(IWhatsAppChannelReader numbers, IFeatureService features) : IModuleHomeCardSource
{
    private static readonly string[] AnyPermission =
        [Permissions.WhatsApp.Customers, Permissions.WhatsApp.Internal, Permissions.WhatsApp.Broadcasts, Permissions.WhatsApp.Manage];

    public async Task<HomeModuleCard?> GetAsync(PendingItemContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!AnyPermission.Any(context.Can) || !await features.IsEnabledAsync(WhatsAppFeature.Key, cancellationToken)) return null;
        return new HomeModuleCard(WhatsAppFeature.Key, await numbers.CountOrganizationAsync(null, cancellationToken));
    }
}
