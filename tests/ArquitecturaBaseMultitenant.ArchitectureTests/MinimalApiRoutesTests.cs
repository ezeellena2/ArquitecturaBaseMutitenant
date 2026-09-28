using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class MinimalApiRoutesTests
{
    // Las rutas de negocio son controllers MVC. Los endpoints de salud los mapea MapDefaultEndpoints en
    // ServiceDefaults (fuera de los proyectos revisados aquí). Si un endpoint técnico necesitara un MapGet,
    // se suma como excepción explícita, con su archivo y su motivo.
    [Theory]
    [InlineData("ArquitecturaBaseMultitenant.Api")]
    [InlineData("ArquitecturaBaseMultitenant.Application")]
    [InlineData("ArquitecturaBaseMultitenant.Infrastructure")]
    public void Project_does_not_map_minimal_api_routes(string project)
    {
        var projectDirectory = Path.Combine(SolutionRoot.FullPath, "src", project);

        var calls = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(projectDirectory, path))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (Path: path, Line: line, Number: index + 1)))
            .Where(entry => !entry.Line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(entry => MinimalApiRoute().IsMatch(entry.Line))
            .Select(entry => $"{Path.GetRelativePath(SolutionRoot.FullPath, entry.Path)}:{entry.Number}");

        Assert.Empty(calls);
    }

    private static bool IsBuildOutput(string projectDirectory, string path) =>
        Path.GetRelativePath(projectDirectory, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    [GeneratedRegex(@"\bMap(?:Get|Post|Put|Delete|Patch|Methods)\b")]
    private static partial Regex MinimalApiRoute();
}
