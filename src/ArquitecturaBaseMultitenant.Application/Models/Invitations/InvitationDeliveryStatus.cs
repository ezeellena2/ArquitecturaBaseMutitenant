namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>
/// El estado de entrega de una invitación pendiente, derivado al leer desde la cola (ADR 0066 §4 y B6): no se guarda, porque
/// la confirmación de la cola corre sin organización. Sin fila en la cola o sin id, <see cref="Sent"/>.
/// </summary>
public enum InvitationDeliveryStatus
{
    Pending,
    Sent,
    /// <summary>No se sabe si salió: «Sin confirmar». Reenviar pide revisar antes con la persona.</summary>
    Uncertain,
    Failed,
}
