using ArquitecturaBaseMultitenant.Application.Models.Home;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Vista previa de Inicio con conteo y límite en el reader, sin cambiar el agregado interno completo.</summary>
public interface IPendingGroupSource : IPendingItemSource
{
    Task<IReadOnlyList<PendingGroup>> ListGroupsAsync(PendingItemContext context, CancellationToken cancellationToken);

    /// <summary>Revalida los grupos con permisos frescos; el alcance de empresa se relee en una consulta acotada.</summary>
    Task<IReadOnlyList<PendingGroup>> RevalidateGroupsAsync(IReadOnlyList<PendingGroup> groups,
        PendingItemContext context, CancellationToken cancellationToken);
}
