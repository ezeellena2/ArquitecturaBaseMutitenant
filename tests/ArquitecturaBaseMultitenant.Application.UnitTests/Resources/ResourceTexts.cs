using System.Collections;
using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Resources;

/// <summary>
/// Todos los textos de un idioma en los resources de Application, del núcleo y de los módulos instalados, sin caer al
/// idioma neutral: el español es la cultura invariante y el inglés, la cultura <c>en</c>.
/// </summary>
internal static class ResourceTexts
{
    public sealed record Text(string Resource, string Key, string Value);

    public static IReadOnlyList<Text> Of(CultureInfo culture)
    {
        var assembly = typeof(ErrorTexts).Assembly;

        return
        [
            .. assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(".resources", StringComparison.Ordinal))
                .Select(name => name[..^".resources".Length])
                .SelectMany(name => Entries(new ResourceManager(name, assembly), culture)
                    .Select(entry => new Text(name, entry.Key, entry.Value))),
        ];
    }

    private static IEnumerable<(string Key, string Value)> Entries(ResourceManager resources, CultureInfo culture)
    {
        using var set = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        if (set is null) yield break;
        foreach (DictionaryEntry entry in set)
            if (entry.Value is string value) yield return ((string)entry.Key, value);
    }
}
