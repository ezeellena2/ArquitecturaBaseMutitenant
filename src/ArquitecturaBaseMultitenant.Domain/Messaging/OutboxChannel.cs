namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>
/// Define la clave del canal de correo del outbox. Los emisores y el dispatcher usan la misma clave para
/// encontrar el transporte registrado.
/// </summary>
public static class OutboxChannel
{
    public const string Email = "email";
}
