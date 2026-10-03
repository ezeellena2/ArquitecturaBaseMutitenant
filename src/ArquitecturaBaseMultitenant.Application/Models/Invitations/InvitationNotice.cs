namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>Los roles van como se guardaron; el canal traduce los de sistema al idioma del mensaje.</summary>
public sealed record InvitationNotice(string OrganizationName, string InviterName, DateTime ExpiresAtUtc,
    string TimeZoneId, string ActionUrl, bool UsesEmailCode, IReadOnlyList<string>? Companies = null,
    IReadOnlyList<string>? Roles = null)
{
    public override string ToString() => "InvitationNotice [redacted]";
}
