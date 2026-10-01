namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>
/// Describe si un mensaje encolado sigue pendiente, se envió, agotó sus intentos o se canceló. El
/// dispatcher usa este estado para decidir qué mensajes puede procesar.
/// </summary>
public enum OutboxStatus
{
    Pending,
    Sent,
    Failed,
    Cancelled,
}
