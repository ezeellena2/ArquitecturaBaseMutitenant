namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>Cómo terminó un intento de entrega, según lo clasificó el transporte (ADR 0066).</summary>
public enum OutboxAttemptOutcome
{
    /// <summary>El proveedor lo aceptó: no significa que llegó.</summary>
    Accepted,

    /// <summary>No salió por algo que puede pasar: vuelve a pendiente con su espera, hasta el tope de intentos.</summary>
    Retry,

    /// <summary>No salió y no cambia al repetir: falla.</summary>
    Rejected,

    /// <summary>No se sabe si salió: queda «Sin confirmar» y no se repite.</summary>
    Uncertain,

    /// <summary>
    /// No salió porque el canal lo posterga sin llamar al transporte (una difusión de una organización con el módulo apagado):
    /// vuelve a pendiente con su espera y no gasta el intento.
    /// </summary>
    Deferred,
}
