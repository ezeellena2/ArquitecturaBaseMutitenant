using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba la versión del SDK, los analizadores y las acciones de CI fijadas por el proyecto. Evita que
/// el entorno de compilación cambie sin una decisión explícita.
/// </summary>
public sealed partial class BuildConfigurationTests
{
    private static readonly Dictionary<string, (string Sha, string Version)> ActionPins =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["checkout"] = ("11bd71901bbe5b1630ceea73d27597364c9af683", "v4.2.2"),
            ["setup-dotnet"] = ("67a3573c9a986a3f9c594539f4ab511d57bb3ce9", "v4.3.1"),
            ["setup-node"] = ("49933ea5288caeca8642d1e84afbd3f7d6820020", "v4.4.0"),
        };

    [Fact]
    public void Sdk_stays_in_the_10_0_400_feature_band()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "global.json")));
        var sdk = document.RootElement.GetProperty("sdk");

        Assert.Equal("10.0.400", sdk.GetProperty("version").GetString());
        Assert.Equal("latestPatch", sdk.GetProperty("rollForward").GetString());
    }

    [Fact]
    public void Analyzer_level_is_explicitly_pinned_to_dotnet_10()
    {
        var document = XDocument.Load(Path.Combine(SolutionRoot.FullPath, "Directory.Build.props"));

        Assert.Equal("10.0-recommended", Assert.Single(document.Descendants("AnalysisLevel")).Value);
    }

    [Fact]
    public void Backend_ci_tests_the_solution_and_pins_official_action_commits()
    {
        var workflow = File.ReadAllText(Path.Combine(SolutionRoot.FullPath, ".github", "workflows", "ci.yml"));

        Assert.Contains("dotnet test --solution ArquitecturaBaseMultitenant.slnx", workflow, StringComparison.Ordinal);
        AssertActionPins(workflow, ["checkout", "setup-dotnet", "setup-node"]);
    }

    [Fact]
    public void Backend_ci_fails_on_unformatted_code_before_building()
    {
        var workflow = File.ReadAllText(Path.Combine(SolutionRoot.FullPath, ".github", "workflows", "ci.yml"));
        var format = workflow.IndexOf("dotnet format ArquitecturaBaseMultitenant.slnx --verify-no-changes", StringComparison.Ordinal);

        Assert.True(format >= 0, "El CI del back no verifica el formato.");
        Assert.True(format < workflow.IndexOf("dotnet build ArquitecturaBaseMultitenant.slnx", StringComparison.Ordinal),
            "El formato se verifica antes de compilar.");
    }

    [Fact]
    public void Front_ci_pins_the_same_official_actions_when_the_sibling_checkout_is_present()
    {
        var front = Path.GetFullPath(Path.Combine(SolutionRoot.FullPath, "..", "ArquitecturaBaseMutitenantFront"));
        var workflowPath = Path.Combine(front, ".github", "workflows", "ci.yml");
        if (!File.Exists(workflowPath))
        {
            Console.WriteLine("Front checkout absent; skipping the cross-repository CI pin check.");
            return;
        }

        AssertActionPins(File.ReadAllText(workflowPath), ["checkout", "setup-node"]);
    }

    /// <summary>
    /// Base de la 12: con el depurador enganchado, el proveedor Debug escribe cada línea en la ventana de salida y frena la
    /// Api; en Development queda en Warning.
    /// </summary>
    [Fact]
    public void Development_keeps_the_debug_logging_provider_at_warning()
    {
        var developmentSettings = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Api", "appsettings.Development.json");
        using var document = JsonDocument.Parse(File.ReadAllText(developmentSettings));

        Assert.Equal("Warning", document.RootElement.GetProperty("Logging").GetProperty("Debug").GetProperty("LogLevel")
            .GetProperty("Default").GetString());
    }

    /// <summary>
    /// Base de la 12: en Windows el cliente de Redis prueba primero <c>::1</c> y pierde unos dos segundos antes de ir a
    /// 127.0.0.1, donde Docker publica Redis; con el segundo de la ADR 0052 no conectaba nunca y la caché quedaba Degraded.
    /// En Development tiene 5 s; los demás ambientes siguen con el segundo, porque <c>appsettings.json</c> no lo cambia.
    /// </summary>
    [Fact]
    public void Development_gives_redis_five_seconds_to_connect()
    {
        var api = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Api");
        using var development = JsonDocument.Parse(File.ReadAllText(Path.Combine(api, "appsettings.Development.json")));
        using var shared = JsonDocument.Parse(File.ReadAllText(Path.Combine(api, "appsettings.json")));

        Assert.Equal(5000, development.RootElement.GetProperty("Cache").GetProperty("ConnectTimeoutMilliseconds").GetInt32());
        Assert.False(shared.RootElement.TryGetProperty("Cache", out var cache)
            && cache.TryGetProperty("ConnectTimeoutMilliseconds", out _));
    }

    [Fact]
    public void Development_web_callback_matches_the_front_route_when_the_sibling_checkout_is_present()
    {
        const string callbackPath = "/auth/callback";
        var front = Path.GetFullPath(Path.Combine(SolutionRoot.FullPath, "..", "ArquitecturaBaseMutitenantFront"));
        var authConfigPath = Path.Combine(front, "src", "auth", "authConfig.ts");
        var routesPath = Path.Combine(front, "src", "app", "routes.tsx");
        if (!File.Exists(authConfigPath) || !File.Exists(routesPath))
        {
            Console.WriteLine("Front checkout absent; skipping the cross-repository OIDC callback check.");
            return;
        }

        var authConfig = File.ReadAllText(authConfigPath);
        Assert.Contains($"export const authCallbackPath = \"{callbackPath}\";", authConfig, StringComparison.Ordinal);
        Assert.Contains("redirect_uri: `${origin}${authCallbackPath}`", authConfig, StringComparison.Ordinal);
        Assert.Contains($"path: \"{callbackPath}\"", File.ReadAllText(routesPath), StringComparison.Ordinal);

        var developmentSettings = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Api", "appsettings.Development.json");
        using var document = JsonDocument.Parse(File.ReadAllText(developmentSettings));
        var redirectUris = document.RootElement.GetProperty("Authentication").GetProperty("Clients")
            .GetProperty("Web").GetProperty("RedirectUris").EnumerateArray()
            .Select(uri => uri.GetString()).ToArray();
        var issuer = Assert.IsType<string>(document.RootElement.GetProperty("Authentication").GetProperty("Issuer").GetString());
        Assert.Equal("https://localtest.me:5174/", issuer);
        var origin = new Uri(issuer).GetLeftPart(UriPartial.Authority);
        Assert.Contains($"{origin}{callbackPath}", redirectUris);
        Assert.DoesNotContain($"{origin}/callback", redirectUris);
        var frontIssuerPath = Path.Combine(front, "src", "auth", "issuer.ts");
        Assert.Contains(issuer, File.ReadAllText(frontIssuerPath), StringComparison.Ordinal);
    }

    /// <summary>
    /// capas-y-flujo.md: CA1502 rompe el build con un método de producción más complejo que el umbral de
    /// <c>CodeMetricsConfig.txt</c>. El umbral nació en 50, el del método más complejo de ese día
    /// (<c>AssistantContractContributor.Describe</c>), y solo baja: subirlo, apagar la regla en otra sección del
    /// <c>.editorconfig</c> o suprimirla en el código es una decisión de arquitectura, no un arreglo.
    /// </summary>
    [Fact]
    public void Cyclomatic_complexity_ratchet_is_on_for_production_code_and_never_rises()
    {
        const int ceiling = 50;
        var thresholds = File.ReadAllLines(Path.Combine(SolutionRoot.FullPath, "CodeMetricsConfig.txt"))
            .Select(line => ComplexityThreshold().Match(line))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.InRange(Assert.Single(thresholds), 1, ceiling);

        var props = XDocument.Load(Path.Combine(SolutionRoot.FullPath, "Directory.Build.props"));
        Assert.Contains(props.Descendants("AdditionalFiles"),
            file => file.Attribute("Include")?.Value == "$(MSBuildThisFileDirectory)CodeMetricsConfig.txt");

        var sections = EditorConfigSections(File.ReadAllLines(Path.Combine(SolutionRoot.FullPath, ".editorconfig")));
        Assert.Contains("dotnet_diagnostic.CA1502.severity = warning", sections["[*.cs]"]);
        Assert.Equal(["[*.cs]", "[tests/**.cs]"],
            sections.Where(section => section.Value.Any(line => line.StartsWith("dotnet_diagnostic.CA1502", StringComparison.Ordinal)))
                .Select(section => section.Key).Order(StringComparer.Ordinal));

        var suppressed = new[] { "src", "tools" }
            .Select(folder => Path.Combine(SolutionRoot.FullPath, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("CA1502", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(SolutionRoot.FullPath, file));
        Assert.Empty(suppressed);
    }

    private static Dictionary<string, List<string>> EditorConfigSections(IEnumerable<string> lines)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal) { [""] = [] };
        var current = sections[""];
        foreach (var line in lines.Select(line => line.Trim()))
        {
            if (line.StartsWith('['))
            {
                if (!sections.TryGetValue(line, out var existing))
                {
                    existing = [];
                    sections[line] = existing;
                }

                current = existing;
            }
            else if (line.Length > 0 && !line.StartsWith('#'))
            {
                current.Add(line);
            }
        }

        return sections;
    }

    private static void AssertActionPins(string workflow, IReadOnlyCollection<string> expectedActions)
    {
        var matches = ActionReference().Matches(workflow);
        Assert.Equal(expectedActions.Count, matches.Count);
        foreach (Match match in matches)
        {
            var action = match.Groups["action"].Value;
            Assert.Contains(action, expectedActions);
            var expected = ActionPins[action];
            Assert.Equal(expected.Sha, match.Groups["sha"].Value);
            Assert.Equal(expected.Version, match.Groups["version"].Value);
        }
    }

    [GeneratedRegex(@"\buses:\s+actions/(?<action>[\w-]+)@(?<sha>[^\s#]+)(?:\s+#\s+(?<version>v[\d.]+))?", RegexOptions.CultureInvariant)]
    private static partial Regex ActionReference();

    [GeneratedRegex(@"^\s*CA1502\s*:\s*(?<value>\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ComplexityThreshold();
}
