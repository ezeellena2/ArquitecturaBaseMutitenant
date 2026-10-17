using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>Proyección interna del alcance autorizado. Los hashes nunca forman parte del contrato HTTP.</summary>
public sealed record InvitationRow(Guid Id, Guid MemberId, Guid? MemberUserId, MemberStatus MemberStatus,
    Guid TenantId, TenantKind TenantKind, TenantStatus TenantStatus, string OrganizationName,
    InvitationIdentity Destination, string Channel, string TokenHash, InvitationStatus Status, DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc, DateTime? AcceptedAtUtc, Guid? AcceptedByUserId, string? BootstrapNonceHash,
    string? InviterName, bool InviterDeleted = false, Guid? InviterUserId = null)
{
    public override string ToString() => $"InvitationRow {{ Id = {Id}, Status = {Status} }}";
}
