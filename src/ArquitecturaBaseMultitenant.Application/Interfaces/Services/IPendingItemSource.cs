using ArquitecturaBaseMultitenant.Application.Models.Home;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Puerto de «Para resolver» (ADR 0040). Cada área, y cada módulo, aporta sus pendientes con una implementación que
/// lee con el tenant del claim y devuelve solo lo que quien mira puede resolver. No guarda nada.
/// </summary>
public interface IPendingItemSource
{
    Task<IReadOnlyList<PendingItem>> ListAsync(PendingItemContext context, CancellationToken cancellationToken);
}
