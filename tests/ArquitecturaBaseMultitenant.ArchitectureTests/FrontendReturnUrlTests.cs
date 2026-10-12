using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>Las rutas de navegación del front se escriben sólo en el catálogo ReturnUrls.</summary>
public sealed class FrontendReturnUrlTests
{
    // Inventario generado del router real, incluidos módulos; el front verifica igualdad exacta.
    // La copia versionada permite ejecutar esta guarda también en el CI que descarga sólo el back.
    private static readonly Regex FrontendRoutes = LoadFrontendRoutes();

    private static Regex LoadFrontendRoutes()
    {
        var routes = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(SolutionRoot.FullPath,
            "docs", "contracts", "frontend-routes.json")))!;
        Assert.Contains("/org/roles/:roleId", routes);
        Assert.Contains("/plataforma/cuentas/:id", routes);
        var patterns = routes.Where(route => route != "/").Distinct().Select(route => string.Join("/", route.Split('/').Select(segment =>
            segment.StartsWith(':') ? "[^/\"?#]+" : Regex.Escape(segment))));
        return new Regex("\"(?:" + string.Join("|", patterns) + ")(?:[?#][^\"]*)?\"|(?:RedirectUri|\\[\"returnUrl\"\\])\\s*=\\s*\"/\"", RegexOptions.CultureInvariant);
    }
    [Fact]
    public void Frontend_paths_are_declared_only_in_ReturnUrls()
    {
        var violations = Directory.EnumerateFiles(Path.Combine(SolutionRoot.FullPath, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && Path.GetFileName(path) != "ReturnUrls.cs")
            .SelectMany(path => FrontendRoutes.Matches(File.ReadAllText(path))
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
    [InlineData("\"/empresas\"", true)]
    [InlineData("\"/terminos\"", true)]
    [InlineData("\"/privacidad\"", true)]
    [InlineData("\"/sin-permiso\"", true)]
    [InlineData("\"/mas\"", true)]
    [InlineData("RedirectUri = \"/\"", true)]
    [InlineData("[\"returnUrl\"] = \"/\"", true)]
    [InlineData("Path = \"/\"", false)]
    [InlineData("\"/api/auth/login\"", false)]
    [InlineData("\"/connect/authorize\"", false)]
    [InlineData("\"//\"", false)]
    public void Detector_distinguishes_frontend_navigation_from_backend_protocol(string source, bool expected) =>
        Assert.Equal(expected, FrontendRoutes.IsMatch(source));
}
