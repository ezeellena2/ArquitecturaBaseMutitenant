namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Las etiquetas que avanzan los cambios guardados en este scope hasta que su transacción termina (ADR 0052). Las junta
/// <see cref="CacheTagCollector"/> al guardar; <see cref="CacheGenerationInterceptor"/> las avanza antes del commit, las
/// invalida en Redis después y las descarta con un rollback.
/// </summary>
internal sealed class PendingCacheTags
{
    private readonly SortedSet<string> _tags = new(StringComparer.Ordinal);

    internal bool IsEmpty => _tags.Count == 0;

    internal void Add(IEnumerable<string> tags) => _tags.UnionWith(tags);

    /// <summary>Las etiquetas en orden: dos transacciones toman los locks de sus generaciones en el mismo orden.</summary>
    internal string[] Take()
    {
        string[] tags = [.. _tags];
        _tags.Clear();
        return tags;
    }

    internal void Clear() => _tags.Clear();
}
