using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.ArchitectureTests.Support;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Detecta catálogos de referencia escritos como listas, comparaciones, switches o enums en código
/// productivo. Exige obtener esos datos de su fuente central.
/// </summary>
public sealed class ReferenceDataHardcodeTests
{
    private enum UnrelatedChoice { ARS }

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
            })
            .Concat(new[] { "Domain", "Application", "Infrastructure", "Api" }
                .Select(name => Assembly.Load("ArquitecturaBaseMultitenant." + name))
                .SelectMany(assembly => ReferenceDataHardcodeScanner.EnumMemberViolations(assembly)
                    .Concat(ReferenceDataHardcodeScanner.IlLiteralViolations(assembly))))
            .Concat(ReferenceDataHardcodeScanner.JsonViolations(sourceRoot));

        var found = violations.ToArray();
        Assert.True(found.Length == 0, string.Join(Environment.NewLine, found));
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

    [Fact]
    public void Enum_members_are_checked_independently_of_enum_name()
    {
        var violations = ReferenceDataHardcodeScanner.EnumMemberViolations(typeof(UnrelatedChoice).Assembly);

        Assert.Contains(violations, value => value.Contains("UnrelatedChoice.ARS", StringComparison.Ordinal));
    }

    [Fact]
    public void Compiled_literals_are_checked_even_if_source_spelling_changes()
    {
        var violations = ReferenceDataHardcodeScanner.IlLiteralViolations(typeof(ReferenceDataHardcodeTests).Assembly);

        Assert.Contains(violations, value => value.Contains(nameof(ReferenceDataHardcodeTests), StringComparison.Ordinal)
            && value.EndsWith(": ARS", StringComparison.Ordinal));
    }

    [Fact]
    public void Only_database_icu_bootstrap_and_its_validator_are_exempt_in_IL()
    {
        Assert.True(ReferenceDataHardcodeScanner.IsAllowedIcuLiteral(new CallSites.Literal(
            "ArquitecturaBaseMultitenant.Infrastructure.Persistence.DatabaseBootstrapExtensions", "es-AR")));
        Assert.True(ReferenceDataHardcodeScanner.IsAllowedIcuLiteral(new CallSites.Literal(
            "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls.RuntimeRoleValidator", "es-AR")));
        Assert.False(ReferenceDataHardcodeScanner.IsAllowedIcuLiteral(new CallSites.Literal("Other", "es-AR")));
    }

    [Fact]
    public void Json_files_outside_the_generated_reference_catalogs_are_checked()
    {
        var root = Path.Combine(Path.GetTempPath(), "reference-hardcodes-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "appsettings.json"), "{\"currency\":\"ARS\"}");
            var approved = Path.Combine(root, "ArquitecturaBaseMultitenant.Infrastructure", "Persistence", "Seed", "ReferenceData");
            Directory.CreateDirectory(approved);
            File.WriteAllText(Path.Combine(approved, "currencies.json"), "{\"currency\":\"USD\"}");

            var violations = ReferenceDataHardcodeScanner.JsonViolations(root).ToArray();

            Assert.Contains(violations, value => value.Contains("appsettings.json", StringComparison.Ordinal)
                && value.Contains("ARS", StringComparison.Ordinal));
            Assert.DoesNotContain(violations, value => value.Contains("USD", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
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

    public static IEnumerable<string> EnumMemberViolations(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type.IsEnum)
            .SelectMany(type => Enum.GetNames(type)
                .Where(CatalogCodes.Contains)
                .Select(member => $"{type.FullName}.{member}"));

    public static IEnumerable<string> IlLiteralViolations(Assembly assembly) =>
        CallSites.Literals(assembly)
            .Where(literal => CatalogCodes.Contains(literal.Value))
            .Where(literal => !IsAllowedIcuLiteral(literal))
            .Select(literal => $"{literal.Owner}: {literal.Value}");

    public static bool IsAllowedIcuLiteral(CallSites.Literal literal) =>
        literal.Value == "es-AR"
        && (literal.Owner == "ArquitecturaBaseMultitenant.Infrastructure.Persistence.DatabaseBootstrapExtensions"
            || literal.Owner == "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls.RuntimeRoleValidator");

    public static IEnumerable<string> JsonViolations(string sourceRoot) =>
        Directory.EnumerateFiles(sourceRoot, "*.json", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(sourceRoot, path)
                .Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin"))
            .Where(path => !IsGeneratedCatalog(Path.GetRelativePath(sourceRoot, path).Replace('\\', '/')))
            .SelectMany(path => FindViolations(File.ReadAllText(path))
                .Select(violation => $"{Path.GetRelativePath(sourceRoot, path).Replace('\\', '/')}: {violation}"));

    private static bool IsGeneratedCatalog(string relativePath) =>
        relativePath.StartsWith("ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData/", StringComparison.Ordinal)
        && Path.GetFileName(relativePath) is "currencies.json" or "countries.json" or "time-zones.json" or "cultures.json" or "tax-id-types.json";

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
