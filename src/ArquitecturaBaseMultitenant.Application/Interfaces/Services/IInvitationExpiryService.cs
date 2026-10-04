using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// El vencimiento de las invitaciones en la auditoría de la organización activa. Lo llama un trabajo en segundo plano:
/// una invitación vence por fecha, sin que nadie haga nada, y su vencimiento queda registrado una sola vez.
/// </summary>
public interface IInvitationExpiryService
{
    /// <summary>
    /// Registra una tanda de invitaciones vencidas, con la fecha en que vencieron y el sistema como actor, y devuelve
    /// cuántas registró; 0 cuando no queda ninguna.
    /// </summary>
    Task<Result<int>> RecordExpiredAsync(CancellationToken cancellationToken);
}
