using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>Solo datos presentables de la invitación autorizada; jamás hashes ni secretos.</summary>
/// <remarks>Los roles de quien invita, las empresas y los roles ofrecidos son los del tablero Invitacion.</remarks>
public sealed record InvitationPreviewResponse(InvitationPreviewState State, Guid? OrganizationId = null,
    string? OrganizationName = null, Email? Email = null, string? InviterName = null,
    DateTime? IssuedAtUtc = null, DateTime? ExpiresAtUtc = null, Email? CurrentAccountEmail = null,
    bool CanUseEmailCode = false, bool CanUseGoogle = false, bool NeedsPersonalLoginMethod = false,
    IReadOnlyList<string>? InviterRoles = null, IReadOnlyList<string>? Companies = null,
    IReadOnlyList<string>? Roles = null, string? CurrentAccountName = null)
{
    public override string ToString() => $"InvitationPreviewResponse {{ State = {State} }}";
}
