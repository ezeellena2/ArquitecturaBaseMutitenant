using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

/// <summary>0064 §11: compara el anillo de esta copia con los controles de la base y escribe los que faltan.</summary>
public interface IAiKeyRingControlService
{
    /// <summary>Los ids de las claves que no coinciden, en orden; vacío si el anillo coincide.</summary>
    Task<Result<IReadOnlyList<Guid>>> CheckAsync(CancellationToken ct);
}
