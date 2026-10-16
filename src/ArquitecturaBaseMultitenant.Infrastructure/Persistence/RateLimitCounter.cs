namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>
/// Un contador durable de una cuota (E9): el respaldo compartido de Redis mientras no responde. La clave nombra el perfil,
/// la ventana y la partición, que nunca es una IP completa; vence al terminar su ventana. Lo escribe
/// <c>RateLimitCounterRepository</c> y lo lee el contador de Redis al recuperarse.
/// </summary>
internal sealed class RateLimitCounter
{
    internal const int KeyMaxLength = 200;

    private RateLimitCounter() => Key = string.Empty;

    public string Key { get; private set; }

    public int Count { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }
}
