using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IInvitationReader
{
    Task<InvitationRow?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
