using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Los nombres de los índices únicos que traduce un caso de uso viven en <c>UniqueKeys</c>: Infrastructure los usa en
/// <c>HasDatabaseName</c> y Application en cada <c>catch</c>. Un nombre escrito a mano deja de coincidir en silencio
/// y el choque llega como 500.
/// </summary>
public sealed class UniqueKeysTests
{
    private static readonly Regex IndexName = new("\"(IX|PK|AK)_", RegexOptions.CultureInvariant);

    [Fact]
    public void Application_names_unique_indexes_only_through_unique_keys()
    {
        var applicationRoot = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Application");
        var violations = Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(applicationRoot, path)
                .Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin"))
            .Where(path => !string.Equals(Path.GetFileName(path), "UniqueKeys.cs", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (Path: path, Line: line, Number: index + 1)))
            .Where(item => IndexName.IsMatch(item.Line))
            .Select(item => $"{Path.GetRelativePath(SolutionRoot.FullPath, item.Path)}:{item.Number}")
            .ToArray();

        Assert.Empty(violations);
    }
}
