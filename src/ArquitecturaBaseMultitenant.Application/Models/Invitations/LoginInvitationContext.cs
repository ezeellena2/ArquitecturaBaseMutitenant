using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>Alcance resuelto por correo; la aceptación vuelve a comprobarlo tras verificar el código.</summary>
public sealed record LoginInvitationContext(Guid InvitationId, Guid TenantId, Email Destination, Guid? ExpectedOwnerId);

public sealed record LoginInvitationRow(Guid InvitationId, Guid MemberId, Guid TenantId, DateTime IssuedAtUtc, string OrganizationName);

public sealed record LoginInvitationsContext(Email Destination, Guid? ExpectedOwnerId,
    IReadOnlyList<LoginInvitationRow> Pending, Guid DefaultsTenantId);
