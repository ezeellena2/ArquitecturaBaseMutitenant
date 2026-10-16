using System.Globalization;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Las partes numéricas de la clave física de la caché compartida (ADR 0052): un formato de cable, nunca algo que lea una
/// persona. Lo usa sólo <see cref="SharedCache"/>.
/// </summary>
internal static class CacheKeyFormat
{
    internal static string Generations(IReadOnlyList<long> generations)
    {
        ArgumentNullException.ThrowIfNull(generations);
        var parts = new string[generations.Count];
        for (var index = 0; index < parts.Length; index++)
            parts[index] = generations[index].ToString(CultureInfo.InvariantCulture);
        return string.Join('-', parts);
    }
}
