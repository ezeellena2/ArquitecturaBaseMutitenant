using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class ReferenceDataHardcodeTests
{
    [Fact]
    public void Rule_detects_catalog_codes_in_lists_comparisons_switches_and_enums()
    {
        const string sample = """
            enum CurrencyChoice { ARS, USD }
            var currencies = new[] { "ARS", "USD" };
            if (country == "AR") return;
            var city = zone switch { "America/Argentina/Buenos_Aires" => "BA", _ => "" };
            var culture = "es-AR";
            """;

        var violations = ReferenceDataHardcodeScanner.FindViolations(sample).ToArray();

        Assert.Contains(violations, value => value.Contains("ARS", StringComparison.Ordinal));
        Assert.Contains(violations, value => value.Contains("USD", StringComparison.Ordinal));
        Assert.Contains(violations, value => value.Contains("AR", StringComparison.Ordinal));
        Assert.Contains(violations, value => value.Contains("America/Argentina/Buenos_Aires", StringComparison.Ordinal));
        Assert.Contains(violations, value => value.Contains("es-AR", StringComparison.Ordinal));
        Assert.Contains(violations, value => value.Contains("enum CurrencyChoice", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_code_does_not_embed_reference_catalogs()
    {
        var sourceRoot = Path.Combine(SolutionRoot.FullPath, "src");
        var violations = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(sourceRoot, path)
                .Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin" or "Migrations"))
            .SelectMany(path =>
            {
                var relativePath = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
                var source = File.ReadAllText(path);
                return ReferenceDataHardcodeScanner.FindViolations(source)
                    .Where(violation => !IsDatabaseLocaleDeclaration(relativePath, source, violation))
                    .Select(violation => $"{relativePath}: {violation}");
            });

        Assert.Empty(violations);
    }

    [Fact]
    public void Only_the_exact_bootstrap_icu_locale_declaration_is_exempt()
    {
        const string bootstrap = "ArquitecturaBaseMultitenant.Infrastructure/Persistence/DatabaseBootstrapExtensions.cs";
        const string declaration = "internal const string DatabaseIcuLocale = \"es-AR\";";

        Assert.True(IsDatabaseLocaleDeclaration(bootstrap, declaration, "line 1: es-AR"));
        Assert.False(IsDatabaseLocaleDeclaration(bootstrap, "var culture = \"es-AR\";", "line 1: es-AR"));
        Assert.False(IsDatabaseLocaleDeclaration("Other.cs", declaration, "line 1: es-AR"));
        Assert.False(IsDatabaseLocaleDeclaration(bootstrap, declaration, "line 2: es-AR"));
    }

    private static bool IsDatabaseLocaleDeclaration(string relativePath, string source, string violation)
    {
        const string bootstrap = "ArquitecturaBaseMultitenant.Infrastructure/Persistence/DatabaseBootstrapExtensions.cs";
        if (!string.Equals(relativePath, bootstrap, StringComparison.Ordinal))
        {
            return false;
        }

        var lines = source.Split('\n');
        return Enumerable.Range(0, lines.Length).Any(index =>
            lines[index].Trim() == "internal const string DatabaseIcuLocale = \"es-AR\";"
            && violation == $"line {index + 1}: es-AR");
    }
}

internal static partial class ReferenceDataHardcodeScanner
{
    private static readonly HashSet<string> CatalogCodes = LoadCodes();

    public static IEnumerable<string> FindViolations(string source)
    {
        var lineNumber = 0;
        foreach (var line in source.Split('\n'))
        {
            lineNumber++;
            foreach (Match match in StringLiteral().Matches(line))
            {
                var value = match.Groups["value"].Value;
                if (CatalogCodes.Contains(value))
                {
                    yield return $"line {lineNumber}: {value}";
                }
            }

            foreach (Match match in ReferenceEnum().Matches(line))
            {
                yield return $"line {lineNumber}: enum {match.Groups["name"].Value}";
            }
        }
    }

    private static HashSet<string> LoadCodes()
    {
        var catalogRoot = Path.Combine(SolutionRoot.FullPath, "src",
            "ArquitecturaBaseMultitenant.Infrastructure", "Persistence", "Seed", "ReferenceData");
        (string File, string Property, string Key)[] catalogs =
        [
            ("currencies.json", "Currencies", "Code"),
            ("countries.json", "Countries", "Code"),
            ("time-zones.json", "TimeZones", "Id"),
            ("cultures.json", "Cultures", "Code"),
            ("tax-id-types.json", "TaxIdTypes", "Code")
        ];

        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (file, property, key) in catalogs)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(catalogRoot, file)));
            foreach (var item in document.RootElement.GetProperty(property).EnumerateArray())
            {
                codes.Add(item.GetProperty(key).GetString()!);
            }
        }

        return codes;
    }

    [GeneratedRegex("\"(?<value>[^\"\\\\]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"\benum\s+(?<name>\w*(?:Currency|Country|TimeZone|Culture|TaxId)\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceEnum();
}
