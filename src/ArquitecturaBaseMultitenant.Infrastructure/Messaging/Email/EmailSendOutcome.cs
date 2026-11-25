namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>
/// Cómo terminó un intento de correo, según el punto de la conversación SMTP donde quedó (N5 y N8). Antes de que el servidor
/// acepte al destinatario no salió nada; después, un corte o la espera vencida no dicen si llegó.
/// </summary>
internal enum EmailSendOutcome
{
    /// <summary>El servidor lo aceptó.</summary>
    Sent,

    /// <summary>No salió: la conexión, el saludo, el remitente o un 4xx antes del cuerpo, o un 4xx que lo rechaza. Se reintenta.</summary>
    NotSent,

    /// <summary>El servidor rechazó las credenciales (un 535): no salió, se reintenta y el chequeo <c>email</c> queda en Degraded.</summary>
    CredentialsRejected,

    /// <summary>Un 5xx al destinatario: esa dirección no lo recibe. No se reintenta.</summary>
    RecipientRejected,

    /// <summary>El servidor contestó que no lo toma (un 5xx), o el correo no se puede armar. No se reintenta.</summary>
    Rejected,

    /// <summary>Se cortó o venció la espera con el destinatario aceptado: pudo haber llegado. No se repite.</summary>
    Uncertain,
}
