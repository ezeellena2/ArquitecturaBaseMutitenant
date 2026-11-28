using ArquitecturaBaseMultitenant.Domain.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>
/// Una invitación por emitir. <c>Consent</c> es el de «Cargar teléfonos» (179): quién confirmó que la persona aceptó los
/// mensajes por WhatsApp, y cuándo; un reenvío conserva el de la invitación que reemplaza. <c>ReservedByCaller</c>: el
/// llamador reserva en la misma transacción el lote entero en los topes del canal (la carga de «Cargar teléfonos», B9), y
/// el emisor no vuelve a contar esta invitación.
/// </summary>
public sealed record IssueInvitationRequest(InvitationIdentity Destination, Guid InviterUserId, string Channel,
    Guid? MemberId = null, Guid? ReplacingInvitationId = null, InvitationConsent? Consent = null, bool ReservedByCaller = false)
{
    public override string ToString() => "IssueInvitationRequest [redacted]";
}
