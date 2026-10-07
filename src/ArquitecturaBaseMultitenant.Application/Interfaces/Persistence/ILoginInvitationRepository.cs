using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Aceptación de reservas por correo probado, bajo RLS nominal y la UoW del ingreso.</summary>
public interface ILoginInvitationRepository
{
    Task<IReadOnlyList<(Invitation Invitation, Member Member)>> LockAndReadAsync(LoginInvitationsContext context,
        CancellationToken ct);
    Task BindVerifiedUserAsync(Guid userId, CancellationToken ct);
}
