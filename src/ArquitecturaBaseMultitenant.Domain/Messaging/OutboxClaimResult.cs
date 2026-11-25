namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>Qué pasó al intentar reservar un mensaje de la cola.</summary>
public enum OutboxClaimResult
{
    /// <summary>Quedó reservado para este intento.</summary>
    Claimed,

    /// <summary>No le toca: no venció su espera, ya no está pendiente o lo tiene otro intento.</summary>
    NotDue,

    /// <summary>Su reserva venció sin respuesta: pasó a «Sin confirmar» (<see cref="OutboxMessage.InterruptedCode"/>).</summary>
    Interrupted,

    /// <summary>Venció su plazo antes de salir: se descartó (<see cref="OutboxMessage.ExpiredCode"/>).</summary>
    Expired,

    /// <summary>Ya no le quedan intentos: falló.</summary>
    Exhausted,
}
