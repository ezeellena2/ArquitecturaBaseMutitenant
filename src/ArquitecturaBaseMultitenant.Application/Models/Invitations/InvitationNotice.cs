namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

public sealed record InvitationNotice(string OrganizationName, string InviterName, DateTime ExpiresAtUtc,
    string TimeZoneId, string ActionUrl, bool UsesEmailCode)
{
    public override string ToString() => "InvitationNotice [redacted]";
}
