using ArquitecturaBaseMultitenant.Domain.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

public sealed record IssueInvitationRequest(InvitationIdentity Destination, Guid InviterUserId, string Channel,
    Guid? MemberId = null, Guid? ReplacingInvitationId = null)
{
    public override string ToString() => "IssueInvitationRequest [redacted]";
}
