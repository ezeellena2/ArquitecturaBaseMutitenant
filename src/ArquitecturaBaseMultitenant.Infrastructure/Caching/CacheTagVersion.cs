namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// La generación durable de una etiqueta de caché (ADR 0052): un entero que avanza en la misma transacción que cambia los
/// datos de la etiqueta. Guarda sólo el alcance y la versión, nunca contenido de negocio. La escribe
/// <see cref="CacheGenerationInterceptor"/> y la lee <see cref="SharedCache"/>.
/// </summary>
internal sealed class CacheTagVersion
{
    private CacheTagVersion() => Tag = string.Empty;

    public string Tag { get; private set; }

    public long Generation { get; private set; }
}
