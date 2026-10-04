using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Los nombres de los índices únicos y de las claves foráneas que traduce un caso de uso viven en <c>UniqueKeys</c> y
/// <c>ForeignKeys</c>: Infrastructure los usa en <c>HasDatabaseName</c> y <c>HasConstraintName</c>, y Application en
/// cada <c>catch</c>. Un nombre escrito a mano deja de coincidir en silencio y el choque llega como 500.
/// </summary>
public sealed class UniqueKeysTests
{
    private static readonly Regex ConstraintName = new("\"(IX|PK|AK|FK)_", RegexOptions.CultureInvariant);

    [Fact]
    public void Application_names_unique_indexes_and_foreign_keys_only_through_their_classes()
    {
        var applicationRoot = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Application");
        var violations = Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(applicationRoot, path)
                .Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin"))
            .Where(path => Path.GetFileName(path) is not ("UniqueKeys.cs" or "ForeignKeys.cs"))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (Path: path, Line: line, Number: index + 1)))
            .Where(item => ConstraintName.IsMatch(item.Line))
            .Select(item => $"{Path.GetRelativePath(SolutionRoot.FullPath, item.Path)}:{item.Number}")
            .ToArray();

        Assert.Empty(violations);
    }
}
