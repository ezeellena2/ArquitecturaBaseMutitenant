using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>Personal: «Mensajes» existe con el módulo instalado. Business: decide el grant de la organización.</summary>
internal sealed class WhatsAppAvailabilityPolicy : IFeatureAvailabilityPolicy
{
    public string Key => WhatsAppFeature.Key;
    public Task<bool?> IsAvailableAsync(Access access, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<bool?>(access switch
        {
            Access.Consumer => true,
            Access.Business => null,
            _ => false,
        });
    }
}
