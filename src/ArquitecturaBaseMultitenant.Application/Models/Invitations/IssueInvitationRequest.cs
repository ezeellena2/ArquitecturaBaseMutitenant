using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

public sealed record IssueInvitationRequest(Email Destination, Guid InviterUserId, string Channel,
    Guid? MemberId = null)
{
    public override string ToString() => "IssueInvitationRequest [redacted]";
}
