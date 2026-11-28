using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Organization;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Observa cambios de acceso o permisos de un miembro dentro de la UoW del núcleo (B12): los chats que tenía tomados en
/// una bandeja que ya no puede atender vuelven a «Sin tomar» en su equipo; los de la bandeja que sí conserva quedan igual.
/// </summary>
internal sealed class WhatsAppMemberAccessObserver(IWhatsAppChatRepository chats, TimeProvider clock) : IMemberAccessObserver
{
    public async Task AccessChangedAsync(Guid userId, IReadOnlyCollection<string> effectivePermissions, CancellationToken ct)
    {
        var canCustomers = effectivePermissions.Contains(Permissions.WhatsApp.Customers, StringComparer.Ordinal);
        var canInternal = effectivePermissions.Contains(Permissions.WhatsApp.Internal, StringComparer.Ordinal);
        if (canCustomers && canInternal) return;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        foreach (var chat in await chats.ListTakenByAsync(userId, ct))
        {
            if (chat.Inbox == WhatsAppInbox.Customers && !canCustomers
                || chat.Inbox == WhatsAppInbox.Internal && !canInternal)
            {
                chat.Transfer(null, null, nowUtc);
            }
        }
    }
}
