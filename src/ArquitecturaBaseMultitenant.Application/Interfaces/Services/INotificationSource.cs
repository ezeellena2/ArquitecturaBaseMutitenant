using ArquitecturaBaseMultitenant.Application.Models.Notifications;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Puerto de la campana (base de la 12), con el alcance de <see cref="IPendingItemSource"/> (ADR 0040). Cada área, y cada
/// módulo, aporta sus avisos con una implementación de sufijo <c>NotificationSource</c> que lee con el alcance de quien mira
/// (el tenant sale del claim) y devuelve solo lo que puede ver: verifica sus permisos, su acceso y, si pertenece a un
/// módulo, su disponibilidad. Entrega cada aviso sin leer. No guarda nada; una fuente que falla no tumba a las demás.
/// </summary>
public interface INotificationSource
{
    Task<IReadOnlyList<NotificationItem>> ListAsync(NotificationContext context, CancellationToken cancellationToken);
}
