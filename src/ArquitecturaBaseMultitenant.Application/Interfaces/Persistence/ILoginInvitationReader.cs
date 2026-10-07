using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Índice técnico global para resolver el alcance de una invitación vigente, sin leer filas privadas.</summary>
public interface ILoginInvitationReader
{
    Task<IReadOnlyList<LoginInvitationRow>> ListPendingAsync(Email destination, DateTime nowUtc, CancellationToken ct);
}
