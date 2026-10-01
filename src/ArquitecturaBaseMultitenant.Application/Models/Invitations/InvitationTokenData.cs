namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

public sealed record InvitationTokenData(Guid TenantId, Guid InvitationId, string Secret)
{
    public override string ToString() => "InvitationTokenData [redacted]";
}
