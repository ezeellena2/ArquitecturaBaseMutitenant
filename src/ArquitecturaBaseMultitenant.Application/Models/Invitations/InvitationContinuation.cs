namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

public sealed record InvitationContinuation(string Token, string Nonce, DateTime ExpiresAtUtc)
{
    public override string ToString() => "InvitationContinuation [redacted]";
}
