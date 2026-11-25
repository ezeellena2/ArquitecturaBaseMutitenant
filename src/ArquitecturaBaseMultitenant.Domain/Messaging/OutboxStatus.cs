namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>
/// Describe si un mensaje encolado sigue pendiente (también mientras espera un reintento), se envió, no salió, se canceló o no
/// se sabe si salió. El dispatcher usa este estado para decidir qué mensajes puede procesar.
/// </summary>
public enum OutboxStatus
{
    Pending,
    Sent,
    Failed,
    Cancelled,

    /// <summary>
    /// «Sin confirmar» (ADR 0066): el transporte pudo haberlo entregado y no hay respuesta que lo confirme. No se repite solo;
    /// lo decide una persona.
    /// </summary>
    Uncertain,
}
