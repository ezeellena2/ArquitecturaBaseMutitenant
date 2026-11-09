using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

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
    [InlineData("\"/invitacion#token\"", true)]
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

    /// <summary>«Otro acceso» (base de la 12): el link vuelve solo a una ruta del acceso elegido, nunca a una ajena.</summary>
    [Theory]
    [InlineData("/org/usuarios/0c4b2f1e-5d0c-4a3e-9a51-4b8a1f0c2d33", Access.Business, true)]
    [InlineData("/org", Access.Business, true)]
    [InlineData("/org/usuarios?estado=Inactive#fila", Access.Business, true)]
    [InlineData("/cuenta", Access.Consumer, true)]
    [InlineData("/", Access.Consumer, true)]
    [InlineData("/plataforma/organizaciones/1", Access.Platform, true)]
    [InlineData("/cuenta", Access.Business, false)]
    [InlineData("/organizaciones", Access.Business, false)]
    [InlineData("/org/usuarios", Access.Consumer, false)]
    [InlineData("/plataforma", Access.Consumer, false)]
    [InlineData("/plataforma/cuentas", Access.Business, false)]
    [InlineData("/login/empresa?returnUrl=/org", Access.Consumer, false)]
    [InlineData("/connect/authorize?access=business", Access.Consumer, false)]
    [InlineData("/auth/callback", Access.Consumer, false)]
    [InlineData("/api/notifications", Access.Consumer, false)]
    [InlineData("//evil.example/org", Access.Business, false)]
    [InlineData("https://evil.example/org", Access.Business, false)]
    [InlineData("/org\\evil", Access.Business, false)]
    [InlineData("/org/\tusuarios", Access.Business, false)]
    [InlineData("", Access.Consumer, false)]
    [InlineData(null, Access.Consumer, false)]
    public void Another_access_link_returns_only_to_a_route_of_the_chosen_access(string? returnTo, Access access, bool expected) =>
        Assert.Equal(expected, ReturnUrls.IsAccessReturn(returnTo, access));

    [Fact]
    public void Another_access_link_does_not_accept_a_destination_longer_than_a_web_address() =>
        Assert.False(ReturnUrls.IsAccessReturn("/org/" + new string('a', TextLimits.WebAddress), Access.Business));
}
