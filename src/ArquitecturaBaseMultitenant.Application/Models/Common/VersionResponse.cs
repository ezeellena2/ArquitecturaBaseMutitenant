using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Models.Common;

/// <summary>
/// La versión que quedó después de un guardado: el xmin que EF trae del mismo UPDATE (o INSERT) que escribió la fila o,
/// en «Tus datos», la versión propia del perfil. El front la toma como base del próximo guardado, nunca la de una
/// relectura (docs/rules/concurrencia.md).
/// </summary>
public sealed record VersionResponse(uint Version)
{
    /// <summary>Se arma después de <c>ExecuteInTransactionAsync</c>, con la entidad que se guardó.</summary>
    public static Result<VersionResponse> Of<TEntity>(Result<TEntity> saved) where TEntity : IVersioned
    {
        ArgumentNullException.ThrowIfNull(saved);
        return saved.IsSuccess ? new VersionResponse(saved.Value.Version) : saved.Error;
    }
}
