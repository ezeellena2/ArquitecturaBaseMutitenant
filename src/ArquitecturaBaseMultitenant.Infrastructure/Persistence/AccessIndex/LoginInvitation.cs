using ArquitecturaBaseMultitenant.Domain.Invitations;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex;

/// <summary>Proyección global mínima; sólo los triggers de Invitations escriben o retiran el destino.</summary>
internal sealed class LoginInvitation
{
    private LoginInvitation() { Destination = null!; }
    public Guid InvitationId { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid TenantId { get; private set; }
    public InvitationIdentity Destination { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
}
