using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// docs/guides/migracion.md, paso 4: el SQL de una migración va en una línea, sin <c>\r</c>. Un literal de varias líneas toma los
/// saltos del archivo, que en Windows con <c>core.autocrlf</c> son CRLF y en Linux LF: el mismo commit mandaría dos SQL distintos,
/// y el cuerpo de una función quedaría distinto en <c>pg_proc</c> según la máquina que armó el bundle.
/// </summary>
public sealed partial class MigrationSqlTests
{
    private static readonly string MigrationsPath = Path.Combine(SolutionRoot.FullPath, "src",
        "ArquitecturaBaseMultitenant.Infrastructure", "Persistence", "Migrations");

    // La regla se escribió con InvitationWhatsAppConsent (2026-11-09); lo de antes ya corrió en todas las bases.
    private const string RuleSince = "20261109003140";

    // Anteriores a este test, de otras etapas, con su SQL ya aplicado: no se reescriben.
    private static readonly string[] Earlier =
    [
        "20261124185047_IdentityDataRetention.cs",
        "20261125053404_AssistantProviderIssue.cs",
        "20261125153522_SettingsRules.cs",
    ];

    [Fact]
    public void Migration_sql_is_one_line()
    {
        var multiline = Directory.EnumerateFiles(MigrationsPath, "*.cs")
            .Select(Path.GetFileName)
            .Where(name => name is not null && !name.EndsWith(".Designer.cs", StringComparison.Ordinal)
                && TimestampedMigration().IsMatch(name) && string.CompareOrdinal(name, RuleSince) >= 0
                && !Earlier.Contains(name, StringComparer.Ordinal))
            .SelectMany(name => RawSql().Matches(File.ReadAllText(Path.Combine(MigrationsPath, name!)))
                .Where(match => match.Groups["sql"].Value.Trim().Contains('\n', StringComparison.Ordinal))
                .Select(match => $"{name}: {match.Groups["sql"].Value.Trim().Split('\n')[0].Trim()}…"))
            .ToArray();

        Assert.Empty(multiline);
    }

    [GeneratedRegex(@"^\d{14}_\w+\.cs$")]
    private static partial Regex TimestampedMigration();

    [GeneratedRegex(@"Sql\(\s*""{3}(?<sql>.*?)""{3}", RegexOptions.Singleline)]
    private static partial Regex RawSql();
}
