using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IInvitationReader
{
    Task<InvitationRow?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Empresas y roles de la persona invitada, primero los de la organización, y los roles de quien invita.</summary>
    Task<InvitationGrantRow> FindGrantAsync(Guid memberId, Guid? inviterUserId, CancellationToken cancellationToken);
}
