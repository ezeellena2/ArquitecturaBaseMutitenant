using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Índice técnico global para resolver el alcance de una invitación vigente, sin leer filas privadas.</summary>
public interface ILoginInvitationReader
{
    Task<IReadOnlyList<LoginInvitationRow>> ListPendingAsync(InvitationIdentity destination, DateTime nowUtc, CancellationToken ct);
}
