using ArquitecturaBaseMultitenant.Domain.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lectura seguida y escrituras de invitaciones dentro del alcance y la UoW del llamador.</summary>
public interface IInvitationRepository
{
    Task LockAsync(Guid invitationId, CancellationToken cancellationToken);
    Task LockDestinationAsync(InvitationIdentity destination, CancellationToken cancellationToken);
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Invitation?> GetPendingByDestinationAsync(InvitationIdentity destination, CancellationToken cancellationToken);
    Task<IReadOnlyList<Invitation>> ListPendingByDestinationsAsync(IReadOnlyCollection<InvitationIdentity> destinations,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Invitation>> ListPendingForMemberAsync(Guid memberId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hasta <paramref name="limit"/> invitaciones que vencieron pendientes y todavía no quedaron en la auditoría
    /// (<see cref="Invitation.RecordExpiry"/>), las más viejas primero. Toma antes un lock de la organización, para
    /// que dos procesos no registren el mismo vencimiento.
    /// </summary>
    Task<IReadOnlyList<Invitation>> ListUnrecordedExpiriesAsync(DateTime nowUtc, int limit,
        CancellationToken cancellationToken);
    void Add(Invitation invitation);
}
