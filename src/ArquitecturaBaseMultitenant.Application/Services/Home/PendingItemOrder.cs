using ArquitecturaBaseMultitenant.Application.Models.Home;

namespace ArquitecturaBaseMultitenant.Application.Services.Home;

/// <summary>Orden de «Para resolver» (ADR 0040): primero lo urgente y después por fecha; lo que no tiene fecha, al final.</summary>
internal static class PendingItemOrder
{
    public static IReadOnlyList<PendingItem> Sort(IEnumerable<PendingItem> items) =>
    [
        .. items.OrderByDescending(item => item.Urgent)
            .ThenBy(item => item.SinceUtc is null)
            .ThenBy(item => item.SinceUtc)
            .ThenBy(item => item.Subject, StringComparer.CurrentCulture),
    ];
}
