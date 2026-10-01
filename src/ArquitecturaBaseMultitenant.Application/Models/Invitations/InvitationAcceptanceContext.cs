using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

internal sealed record InvitationAcceptanceContext(InvitationTokenData Proof, string Token, string TokenHash,
    string Nonce, Email Destination, Guid? ExpectedOwnerId, Guid? CurrentUserId)
{
    public override string ToString() => "InvitationAcceptanceContext [redacted]";
}

internal sealed record InvitationAcceptanceOutcome(Guid UserId, Guid OrganizationId, bool CreatedAccount,
    DateTime ExpiresAtUtc);
