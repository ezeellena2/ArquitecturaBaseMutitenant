using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>Las rutas de navegación del front se escriben sólo en el catálogo ReturnUrls.</summary>
public sealed partial class FrontendReturnUrlTests
{
    [Fact]
    public void Frontend_paths_are_declared_only_in_ReturnUrls()
    {
        var violations = Directory.EnumerateFiles(Path.Combine(SolutionRoot.FullPath, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && Path.GetFileName(path) != "ReturnUrls.cs")
            .SelectMany(path => FrontendLiteral().Matches(File.ReadAllText(path))
                .Select(match => $"{Path.GetRelativePath(SolutionRoot.FullPath, path)}: {match.Value}"))
            .ToArray();
        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData("\"/cuenta?google=linked\"", true)]
    [InlineData("\"/registro/empresa\"", true)]
    [InlineData("\"/org\"", true)]
    [InlineData("\"/plataforma/cuentas\"", true)]
    [InlineData("\"/login/enlace#token\"", true)]
    [InlineData("\"/auth/callback\"", true)]
    [InlineData("\"/silent-renew.html\"", true)]
    [InlineData("RedirectUri = \"/\"", true)]
    [InlineData("[\"returnUrl\"] = \"/\"", true)]
    [InlineData("Path = \"/\"", false)]
    [InlineData("\"/api/auth/login\"", false)]
    [InlineData("\"/connect/authorize\"", false)]
    [InlineData("\"//\"", false)]
    public void Detector_distinguishes_frontend_navigation_from_backend_protocol(string source, bool expected) =>
        Assert.Equal(expected, FrontendLiteral().IsMatch(source));

    [GeneratedRegex("\"/(?:cuenta|login|registro|org|plataforma|invitacion|recuperar|legales|perfiles|auth/callback|silent-renew\\.html)(?=[/\\\"?#])[^\\\"]*\"|(?:RedirectUri|\\[\"returnUrl\"\\])\\s*=\\s*\"/\"", RegexOptions.CultureInvariant)]
    private static partial Regex FrontendLiteral();
}
