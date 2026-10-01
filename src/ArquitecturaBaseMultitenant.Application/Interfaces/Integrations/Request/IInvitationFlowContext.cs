using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Identidad válida y continuación protegida del navegador, sin seleccionar un acceso ni un tenant.</summary>
public interface IInvitationFlowContext
{
    Task<Guid?> GetUserIdAsync(CancellationToken cancellationToken);
    InvitationContinuation? ReadContinuation();
    void Remember(InvitationContinuation continuation);
    void ClearContinuation();
    Task<bool> RestoreSessionAsync(Guid userId, CancellationToken cancellationToken);
}
