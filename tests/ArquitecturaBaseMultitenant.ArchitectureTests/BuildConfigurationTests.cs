using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

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
}
