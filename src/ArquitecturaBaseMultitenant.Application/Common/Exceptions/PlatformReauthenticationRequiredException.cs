namespace ArquitecturaBaseMultitenant.Application.Common.Exceptions;

/// <summary>
/// La transacción de una acción sensible de Plataforma ya no encontró la prueba reciente vigente (E9): venció, se
/// cerró la sesión o cambió el sello entre la guarda HTTP y la escritura. La API responde 403
/// Auth.Platform.ReauthenticationRequired y la reserva de idempotencia se libera, así el reenvío con la misma clave
/// vuelve a ejecutar después de confirmar.
/// </summary>
public sealed class PlatformReauthenticationRequiredException : Exception
{
    public PlatformReauthenticationRequiredException()
        : base("A recent second-factor confirmation is required for this action.")
    {
    }

    public PlatformReauthenticationRequiredException(string message)
        : base(message)
    {
    }

    public PlatformReauthenticationRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
