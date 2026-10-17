using ArquitecturaBaseMultitenant.Domain.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>Alcance resuelto por correo; la aceptación vuelve a comprobarlo tras verificar el código.</summary>
public sealed record LoginInvitationContext(Guid InvitationId, Guid TenantId, InvitationIdentity Destination, Guid? ExpectedOwnerId);

public sealed record LoginInvitationRow(Guid InvitationId, Guid MemberId, Guid TenantId, DateTime IssuedAtUtc, string OrganizationName);

public sealed record LoginInvitationsContext(InvitationIdentity Destination, Guid? ExpectedOwnerId,
    IReadOnlyList<LoginInvitationRow> Pending, Guid DefaultsTenantId);
