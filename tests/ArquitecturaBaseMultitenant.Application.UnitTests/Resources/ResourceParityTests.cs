using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Resources;

/// <summary>Los recursos visibles tienen las mismas claves y argumentos en ambos idiomas.</summary>
public sealed class ResourceParityTests
{
    [Fact]
    public void Error_texts_have_the_same_keys_and_placeholders()
    {
        AssertSameKeysAndPlaceholders(ErrorTexts.ResourceManager);
    }

    [Fact]
    public void Validation_texts_have_the_same_keys_and_placeholders()
    {
        AssertSameKeysAndPlaceholders(ValidationTexts.ResourceManager);
    }

    [Fact]
    public void Placeholder_extraction_ignores_escaped_braces_and_preserves_arguments()
    {
        Assert.Equal(["0", "1"], Placeholders("{1} y {0:N2}, pero {{2}} es literal"));
    }

    private static void AssertSameKeysAndPlaceholders(ResourceManager resourceManager)
    {
        var spanish = Entries(resourceManager, CultureInfo.InvariantCulture);
        var english = Entries(resourceManager, CultureInfo.GetCultureInfo("en"));

        Assert.NotEmpty(spanish);
        Assert.Equal(spanish.Keys.Order(StringComparer.Ordinal), english.Keys.Order(StringComparer.Ordinal));

        foreach (var key in spanish.Keys)
        {
            Assert.Equal(Placeholders(spanish[key]), Placeholders(english[key]));
        }
    }

    private static Dictionary<string, string> Entries(ResourceManager resourceManager, CultureInfo culture)
    {
        var resourceSet = resourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"No resources for culture '{culture.Name}'.");

        return resourceSet.Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.Ordinal);
    }

    private static string[] Placeholders(string text) =>
        Regex.Matches(text, @"(?<!\{)\{(?<index>\d+)(?:,[^}]*)?(?::[^}]*)?\}(?!\})")
            .Select(match => match.Groups["index"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
