using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Consulta global acotada al estado de una identidad, sin exponer ApplicationUser.</summary>
public interface IUserStatusReader
{
    Task<UserStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> ListActiveAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Consulta en lote; una identidad ausente se trata como baja iniciada.</summary>
    Task<Result<IReadOnlyList<UserDeletionStateRow>>> ListDeletionStatesAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Protege todas las identidades con locks de fila compartidos en la transacción del llamador.</summary>
    Task<Result<IReadOnlyList<UserDeletionStateRow>>> LockDeletionStatesAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

/// <summary>Señal neutral: un lease presente (incluso vencido) o Deleted indica baja iniciada.</summary>
public sealed record UserDeletionStateRow(Guid UserId, bool HasDeletionStarted);
