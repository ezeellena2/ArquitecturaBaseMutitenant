using ArquitecturaBaseMultitenant.Domain.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>
/// Una invitación por emitir. <c>Consent</c> es el de «Cargar teléfonos» (179): quién confirmó que la persona aceptó los
/// mensajes por WhatsApp, y cuándo; un reenvío conserva el de la invitación que reemplaza.
/// </summary>
public sealed record IssueInvitationRequest(InvitationIdentity Destination, Guid InviterUserId, string Channel,
    Guid? MemberId = null, Guid? ReplacingInvitationId = null, InvitationConsent? Consent = null)
{
    public override string ToString() => "IssueInvitationRequest [redacted]";
}
