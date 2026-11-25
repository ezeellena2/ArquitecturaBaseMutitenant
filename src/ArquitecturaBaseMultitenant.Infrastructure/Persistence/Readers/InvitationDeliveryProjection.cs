using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// El estado de entrega de una invitación pendiente, derivado al leer del mensaje de la cola que la lleva (B6, ADR 0066 §4),
/// como el de un mensaje en <c>WhatsAppConversationReader</c>: no se guarda en la invitación, porque la confirmación de la
/// cola corre sin organización. Es neutral: el correo y WhatsApp usan la misma regla.
/// </summary>
internal static class InvitationDeliveryProjection
{
    /// <summary>
    /// En la cola, <see cref="InvitationDeliveryStatus.Pending"/>; salió, <see cref="InvitationDeliveryStatus.Sent"/>;
    /// «Sin confirmar», <see cref="InvitationDeliveryStatus.Uncertain"/>; no salió (agotó sus intentos, se canceló o venció),
    /// <see cref="InvitationDeliveryStatus.Failed"/>. Sin fila en la cola (sin id o ya purgada), enviada.
    /// </summary>
    public static InvitationDeliveryStatus FromOutbox(OutboxStatus? status) => status switch
    {
        OutboxStatus.Pending => InvitationDeliveryStatus.Pending,
        OutboxStatus.Uncertain => InvitationDeliveryStatus.Uncertain,
        OutboxStatus.Failed or OutboxStatus.Cancelled => InvitationDeliveryStatus.Failed,
        _ => InvitationDeliveryStatus.Sent,
    };
}
