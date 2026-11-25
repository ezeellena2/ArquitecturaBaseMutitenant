using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

/// <summary>0064 §11: los controles del anillo (<c>platform.AiKeyRingControls</c>), globales y sin borrado.</summary>
public interface IAiKeyRingControlRepository
{
    /// <summary>Serializa dos copias que arrancan a la vez: la segunda lee lo que escribió la primera.</summary>
    Task LockAsync(CancellationToken ct);
    Task<IReadOnlyList<AiKeyRingControl>> ListAsync(CancellationToken ct);
    void Add(AiKeyRingControl control);
}
