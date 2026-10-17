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
    public void Formatting_texts_have_the_same_keys_and_placeholders()
    {
        AssertSameKeysAndPlaceholders(FormattingTexts.ResourceManager);
    }

    [Fact]
    public void Notification_texts_have_the_same_keys_and_placeholders()
    {
        AssertSameKeysAndPlaceholders(NotificationTexts.ResourceManager);
    }

    [Fact]
    public void Audit_texts_have_the_same_keys_and_placeholders()
    {
        AssertSameKeysAndPlaceholders(AuditTexts.ResourceManager);
    }

    [Fact]
    public void Installed_module_texts_have_the_same_keys_and_placeholders()
    {
        var assembly = typeof(ErrorTexts).Assembly;
        var modules = assembly.GetManifestResourceNames().Where(name => name.Contains(".Modules.", StringComparison.Ordinal)
            && name.EndsWith(".resources", StringComparison.Ordinal)).ToArray();
        var expected = new List<string>();
        if (assembly.GetType("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.WhatsAppModule") is not null)
            expected.Add("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources.WhatsAppTexts.resources");
        if (assembly.GetType("ArquitecturaBaseMultitenant.Application.Modules.Assistant.AssistantModule") is not null)
            expected.AddRange(new[] { "Assistant", "AssistantTexts", "AssistantContractData" }
                .Select(name => "ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources." + name + ".resources"));
        Assert.Equal(expected.Order(StringComparer.Ordinal), modules.Order(StringComparer.Ordinal));
        foreach (var resource in modules)
            AssertSameKeysAndPlaceholders(new ResourceManager(resource[..^".resources".Length], assembly));
    }

    [Fact]
    public void Placeholder_extraction_ignores_escaped_braces_and_preserves_arguments()
    {
        Assert.Equal(["0", "1"], Placeholders("{1} y {0:N2}, pero {{2}} es literal"));
        Assert.Equal(["limit", "time"], Placeholders("Llegaste a las {limit} consultas. Se renuevan a las {time}."));
        Assert.Empty(Placeholders("{\"clave\": 1}"));
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
        Regex.Matches(text, @"(?<!\{)\{(?<index>\d+|[A-Za-z_][A-Za-z0-9_]*)(?:,[^}]*)?(?::[^}]*)?\}(?!\})")
            .Select(match => match.Groups["index"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
