namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Una entrada del inventario de ADR 0052: su clave lógica con alcance (de <see cref="CacheKeys"/>), las etiquetas cuyas
/// generaciones entran en la clave física y su vencimiento, que sólo administra espacio.
/// </summary>
internal sealed record CacheEntry(string Key, string[] Tags, TimeSpan Expiration);

/// <summary>
/// Lo que guarda Redis: la versión del esquema, las generaciones con las que se leyó y un DTO permitido. Un sobre de otro
/// esquema u otras generaciones se trata como un miss.
/// </summary>
internal sealed record CacheEnvelope<TValue>(int SchemaVersion, string Generations, TValue Value);
