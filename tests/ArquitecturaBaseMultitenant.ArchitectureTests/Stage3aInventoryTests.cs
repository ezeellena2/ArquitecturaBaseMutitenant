namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class Stage3aInventoryTests
{
    [Fact]
    public void Identity_feature_and_first_area_pointers_are_present()
    {
        var root = SolutionRoot.FullPath;
        var feature = Path.Combine(root, "docs", "features", "identidad.md");

        Assert.True(File.Exists(feature), "The identity feature document is missing.");
        Assert.Contains("## 3a · Ingreso", File.ReadAllText(feature), StringComparison.Ordinal);

        foreach (var folder in new[]
        {
            "src/ArquitecturaBaseMultitenant.Domain/Users",
            "src/ArquitecturaBaseMultitenant.Domain/Authentication",
            "src/ArquitecturaBaseMultitenant.Infrastructure/Identity",
            "tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity",
            "tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication",
        })
        {
            var fullFolder = Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar));
            var agents = Path.Combine(fullFolder, "AGENTS.md");
            var claude = Path.Combine(fullFolder, "CLAUDE.md");

            Assert.True(File.Exists(agents), $"{folder}/AGENTS.md is missing.");
            Assert.Contains("docs/features/identidad.md", File.ReadAllText(agents), StringComparison.Ordinal);
            Assert.True(File.Exists(claude), $"{folder}/CLAUDE.md is missing.");
            Assert.Equal("@AGENTS.md", File.ReadAllText(claude).Trim());
        }
    }
}
