using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lectura seguida y escrituras de invitaciones dentro del alcance y la UoW del llamador.</summary>
public interface IInvitationRepository
{
    Task LockAsync(Guid invitationId, CancellationToken cancellationToken);
    Task LockDestinationAsync(Email destination, CancellationToken cancellationToken);
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Invitation?> GetPendingByDestinationAsync(Email destination, CancellationToken cancellationToken);
    void Add(Invitation invitation);
}
