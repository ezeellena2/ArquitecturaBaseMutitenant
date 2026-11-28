using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Organization;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Observa cambios de acceso o permisos de los miembros dentro de la UoW del núcleo (B12): los chats que tenía tomados cada
/// persona en una bandeja que ya no puede atender vuelven a «Sin tomar» en su equipo; los de la bandeja que sí conserva quedan
/// igual. Toma el lock de cada persona antes de leer sus chats, el mismo que toma «Transferir», y lee los de todas en una
/// consulta: una transferencia que pasó su control antes espera a este cambio, y una que llega después ya no la ve habilitada.
/// </summary>
internal sealed class WhatsAppMemberAccessObserver(IWhatsAppChatRepository chats, TimeProvider clock) : IMemberAccessObserver
{
    public async Task AccessChangedAsync(IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> effectivePermissionsByUser,
        CancellationToken ct)
    {
        Guid[] limited = [.. effectivePermissionsByUser
            .Where(user => !CanAttend(user.Value, WhatsAppInbox.Customers) || !CanAttend(user.Value, WhatsAppInbox.Internal))
            .Select(user => user.Key)];
        if (limited.Length == 0) return;
        await chats.LockAssigneesAsync(limited, ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        foreach (var chat in await chats.ListTakenByAsync(limited, ct))
        {
            if (chat.AssigneeUserId is { } userId && !CanAttend(effectivePermissionsByUser[userId], chat.Inbox))
                chat.Transfer(null, null, nowUtc);
        }
    }

    /// <summary>La misma regla que para tomar un chat: «whatsapp.customers» o «whatsapp.internal», según la bandeja.</summary>
    private static bool CanAttend(IReadOnlyCollection<string> permissions, WhatsAppInbox inbox) =>
        permissions.Contains(AttendPermission(inbox), StringComparer.Ordinal);

    internal static string AttendPermission(WhatsAppInbox inbox) =>
        inbox == WhatsAppInbox.Internal ? Permissions.WhatsApp.Internal : Permissions.WhatsApp.Customers;
}
