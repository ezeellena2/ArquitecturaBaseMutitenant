using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>Solo datos presentables de la invitación autorizada; jamás hashes ni secretos.</summary>
public sealed record InvitationPreviewResponse(InvitationPreviewState State, Guid? OrganizationId = null,
    string? OrganizationName = null, Email? Email = null, string? InviterName = null,
    DateTime? IssuedAtUtc = null, DateTime? ExpiresAtUtc = null, string? CurrentAccountEmail = null,
    bool CanUseEmailCode = false, bool CanUseGoogle = false, bool NeedsPersonalLoginMethod = false)
{
    public override string ToString() => $"InvitationPreviewResponse {{ State = {State} }}";
}
