using System.Text;
using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que las reglas, sus referencias y los tests del arnés correspondan a entregables existentes.
/// Incluye el inventario de pruebas C#, Node y TypeScript.
/// </summary>
public sealed class HarnessTests
{
    private static readonly string Root = SolutionRoot.FullPath;
    private static readonly string FrontRoot = Path.GetFullPath(Path.Combine(Root, "..", "ArquitecturaBaseMutitenantFront"));
    private static readonly string HarnessDocument = Path.Combine(Root, "docs", "architecture", "arnes.md");

    [Fact]
    public void Test_inventory_discovers_Node_and_TypeScript_in_available_checkouts()
    {
        var backend = Path.Combine(Path.GetTempPath(), "harness-back-" + Guid.NewGuid().ToString("N"));
        var frontend = Path.Combine(Path.GetTempPath(), "harness-front-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(backend, "tests"));
            Directory.CreateDirectory(Path.Combine(backend, "scripts"));
            Directory.CreateDirectory(Path.Combine(frontend, "src"));
            File.WriteAllText(Path.Combine(backend, "scripts", "generator.test.mjs"), "test('works', () => {});");
            File.WriteAllText(Path.Combine(frontend, "src", "data.test.ts"), "it('works', () => {});");
            File.WriteAllText(Path.Combine(frontend, "src", "screen.test.tsx"), "it('works', () => {});");

            var names = TestNames(backend, frontend);

            Assert.Contains("generator.test.mjs", names);
            Assert.Contains("data.test.ts", names);
            Assert.Contains("screen.test.tsx", names);
        }
        finally
        {
            if (Directory.Exists(backend)) Directory.Delete(backend, recursive: true);
            if (Directory.Exists(frontend)) Directory.Delete(frontend, recursive: true);
        }
    }

    [Fact]
    public void CSharp_test_class_without_Fact_or_Theory_is_rejected()
    {
        var backend = Path.Combine(Path.GetTempPath(), "harness-csharp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var tests = Path.Combine(backend, "tests");
            Directory.CreateDirectory(tests);
            File.WriteAllText(Path.Combine(tests, "Probe.cs"), """
                public sealed class EmptyProbeTests
                {
                    // [Fact] no es una prueba.
                    public void Helper() { }
                }
                public sealed class ActiveProbeTests
                {
                    [Theory]
                    [InlineData(1)]
                    public void Works(int value) { }
                }
                """);

            var empty = EmptyCSharpTestClasses(backend).ToArray();

            Assert.Contains(empty, value => value.Contains("EmptyProbeTests", StringComparison.Ordinal));
            Assert.DoesNotContain(empty, value => value.Contains("ActiveProbeTests", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(backend)) Directory.Delete(backend, recursive: true);
        }
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("namespace Probes;", "namespace Probes {", "}")]
    [InlineData("namespace Probes.Nested;", "namespace Probes { namespace Nested {", "} }")]
    [InlineData("namespace Probes; partial class Outer {", "namespace Probes { partial class Outer {", "} }", "}")]
    [InlineData("partial struct Outer<T> {", "partial struct Outer<U> {", "}", "}")]
    [InlineData("partial record Outer {", "partial record Outer {", "}", "}")]
    public void CSharp_partial_test_class_uses_a_direct_test_in_another_part(
        string helperPrefix, string testPrefix, string testSuffix, string helperSuffix = "")
    {
        WithCSharpTestFiles([
            ("Probe.Helpers.cs", helperPrefix + "\npublic sealed partial class ProbeTests { }\n" + helperSuffix),
            ("Probe.cs", testPrefix + "\npublic sealed partial class ProbeTests {\n[Fact]\npublic void Works() { }\n}\n" + testSuffix)
        ], backend =>
        {
            Assert.Empty(EmptyCSharpTestClasses(backend));
            Assert.Contains("ProbeTests", TestNames(backend, null));
        });
    }

    [Theory]
    [InlineData("partial class", "class", "", "", "", "")]
    [InlineData("class", "partial class", "", "", "", "")]
    [InlineData("class", "class", "", "", "", "")]
    [InlineData("partial class", "partial class", "namespace One;", "namespace Two;", "", "")]
    [InlineData("partial class", "partial class", "namespace One {", "namespace Two {", "}", "}")]
    [InlineData("partial class", "partial class", "partial class One {", "partial class Two {", "}", "}")]
    [InlineData("partial class", "partial class", "class Outer {", "class Outer {", "}", "}")]
    [InlineData("partial class", "partial class", "partial class Outer<T> {", "partial class Outer<T, U> {", "}", "}")]
    [InlineData("partial class", "partial class", "partial class Outer {", "partial struct Outer {", "}", "}")]
    [InlineData("file partial class", "file partial class", "", "", "", "")]
    public void CSharp_partial_test_class_is_not_rescued_by_a_different_type(
        string helperKind, string testKind, string helperPrefix, string testPrefix, string helperSuffix, string testSuffix)
    {
        WithCSharpTestFiles([
            ("Empty.cs", helperPrefix + "\n" + helperKind + " ProbeTests { }\n" + helperSuffix),
            ("Active.cs", testPrefix + "\n" + testKind + " ProbeTests {\n[Theory]\npublic void Works() { }\n}\n" + testSuffix)
        ], backend =>
        {
            var failure = Assert.Single(EmptyCSharpTestClasses(backend));
            Assert.Contains("tests/Empty.cs: ProbeTests", failure, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CSharp_partial_test_class_is_not_rescued_by_another_project()
    {
        WithCSharpTestFiles([
            ("One/One.csproj", "<Project />"),
            ("One/Modules/Empty.cs", "namespace Probes; partial class ProbeTests { }"),
            ("Two/Two.csproj", "<Project />"),
            ("Two/Active.cs", "namespace Probes; partial class ProbeTests {\n[Fact]\npublic void Works() { }\n}")
        ], backend => Assert.Contains("One/Modules/Empty.cs", Assert.Single(EmptyCSharpTestClasses(backend)), StringComparison.Ordinal));
    }

    [Fact]
    public void CSharp_partial_test_class_joins_parts_below_the_same_project()
    {
        WithCSharpTestFiles([
            ("One/One.csproj", "<Project />"),
            ("One/Modules/Empty.cs", "namespace Probes; partial class ProbeTests { }"),
            ("One/Active.cs", "namespace Probes; partial class ProbeTests {\n[Fact]\npublic void Works() { }\n}")
        ], backend => Assert.Empty(EmptyCSharpTestClasses(backend)));
    }

    [Fact]
    public void CSharp_partial_test_class_ignores_nested_tests_comments_and_strings()
    {
        WithCSharpTestFiles([
            ("Empty.cs", "partial class ProbeTests { }"),
            ("Decoys.cs", """"
                partial class ProbeTests
                {
                    // [Fact] public void Comment() { }
                    /*
                    [Fact]
                    public void BlockComment() { }
                    */
                    string text = "[Fact] public void String() { }";
                    string verbatim = @"
                    [Fact]
                    public void Verbatim() { }";
                    string raw = """
                    [Theory]
                    public void Raw() { }
                    """;
                    class NestedTests
                    {
                        [Fact]
                        public void Works() { }
                    }
                }
                """")
        ], backend =>
        {
            var failures = EmptyCSharpTestClasses(backend).ToArray();
            Assert.Equal(2, failures.Length);
            Assert.All(failures, failure => Assert.Contains("ProbeTests", failure, StringComparison.Ordinal));
            Assert.DoesNotContain("ProbeTests", TestNames(backend, null));
            Assert.Contains("NestedTests", TestNames(backend, null));
        });
    }

    [Fact]
    public void CSharp_partial_test_class_joins_file_local_parts_in_the_same_file()
    {
        WithCSharpTestFiles([
            ("Probe.cs", """
                file partial class ProbeTests { }
                file partial class ProbeTests
                {
                    [Fact]
                    public void Works() { }
                }
                """)
        ], backend => Assert.Empty(EmptyCSharpTestClasses(backend)));
    }

    [Fact]
    public void CSharp_record_class_without_a_direct_test_is_rejected()
    {
        WithCSharpTestFiles([
            ("Empty.cs", "public record class EmptyProbeTests { }")
        ], backend => Assert.Contains("EmptyProbeTests", Assert.Single(EmptyCSharpTestClasses(backend)), StringComparison.Ordinal));
    }

    [Fact]
    public void CSharp_record_class_partial_uses_a_direct_test_in_another_part()
    {
        WithCSharpTestFiles([
            ("Probe.Helpers.cs", "namespace Probes; public partial record class ProbeTests { }"),
            ("Probe.cs", """
                namespace Probes;
                public partial record class ProbeTests
                {
                    [Fact]
                    public void Works() { }
                }
                """)
        ], backend =>
        {
            Assert.Empty(EmptyCSharpTestClasses(backend));
            Assert.Contains("ProbeTests", TestNames(backend, null));
        });
    }

    private static void WithCSharpTestFiles((string Path, string Source)[] files, Action<string> verify)
    {
        var backend = Path.Combine(Path.GetTempPath(), "harness-partial-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var (path, source) in files)
            {
                var file = Path.Combine(backend, "tests", path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, source);
            }
            verify(backend);
        }
        finally
        {
            if (Directory.Exists(backend)) Directory.Delete(backend, recursive: true);
        }
    }

    [Fact]
    public void Lo_verifica_requires_closed_stage_Node_and_TypeScript_tests()
    {
        var line = "- `missing.test.mjs` (E1), `missing.test.ts` (E1), `missing.test.tsx` (E1), `present.lifetime.test.tsx` (E1), `missing.lifetime.test.tsx` (E1), `future.test.tsx` (E3).";
        var testNames = new HashSet<string>(StringComparer.Ordinal) { "present.lifetime.test.tsx" };
        var failures = MissingVerificationTests(line, 7, "docs/rules/probe.md", testNames,
            new HashSet<int> { 0, 1, 2 }).ToArray();

        Assert.Contains(failures, value => value.Contains("missing.test.mjs", StringComparison.Ordinal));
        Assert.Contains(failures, value => value.Contains("missing.test.ts", StringComparison.Ordinal));
        Assert.Contains(failures, value => value.Contains("missing.test.tsx", StringComparison.Ordinal));
        Assert.Contains(failures, value => value.Contains("missing.lifetime.test.tsx", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, value => value.Contains("present.lifetime.test.tsx", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, value => value.Contains("future.test.tsx", StringComparison.Ordinal));
    }

    [Fact]
    public void Front_test_names_are_skipped_when_sibling_checkout_is_absent()
    {
        var backend = Path.Combine(Path.GetTempPath(), "harness-back-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(backend, "scripts"));
            File.WriteAllText(Path.Combine(backend, "scripts", "generator.test.mjs"), "test('works', () => {});");
            var names = TestNames(backend, frontendRoot: null);
            var line = "- `generator.test.mjs` (E1), `missing.test.mjs` (E1), `formatters.test.ts` (E1), `screen.test.tsx` (E1).";

            var failures = MissingVerificationTests(line, 7, "docs/rules/probe.md", names,
                new HashSet<int> { 0, 1, 2 }, frontExists: false).ToArray();

            Assert.Single(failures);
            Assert.Contains("missing.test.mjs", failures[0], StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(backend)) Directory.Delete(backend, recursive: true);
        }
    }

    [Fact]
    public void Persistence_stage_is_closed()
    {
        var path = Path.Combine(Root, "tests", "ArquitecturaBaseMultitenant.ArchitectureTests", "HarnessStage.cs");
        var declaration = Regex.Match(File.ReadAllText(path), @"ClosedStages\s*=\s*\[(?<stages>[^\]]*)\]", RegexOptions.CultureInvariant);
        Assert.True(declaration.Success, "HarnessStage debe enumerar las etapas cerradas para permitir saltos.");
        var closedStages = Regex.Matches(declaration.Groups["stages"].Value, @"\d+", RegexOptions.CultureInvariant)
            .Select(match => int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        Assert.Contains(2, closedStages);
        Assert.Contains(4, closedStages);
        Assert.Contains(5, closedStages);
        Assert.Contains(6, closedStages);
    }

    [Fact]
    public void Root_and_existing_mapped_folders_have_both_agent_files()
    {
        var folders = new[] { Root }
            .Concat(MappedFolders())
            .Concat(FeatureFolders())
            .Concat(AreaFolders())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var missing = folders.SelectMany(MissingPointerFiles).ToArray();

        AssertNoFailures(missing);
    }

    [Fact]
    public void Agent_files_are_short_and_their_links_work()
    {
        var failures = new List<string>();

        foreach (var file in AgentFiles())
        {
            var lines = File.ReadAllLines(file);
            var limit = file == Path.Combine(Root, "AGENTS.md") ? 120 : 8;
            if (lines.Length > limit)
            {
                failures.Add($"{Relative(file)}: {lines.Length} líneas; máximo {limit}.");
            }

            failures.AddRange(BrokenMarkdownLinks(file, lines));
            failures.AddRange(BrokenCopyReferences(file, lines.Where(line => line.Contains("Copiá de:", StringComparison.Ordinal))));
        }

        AssertNoFailures(failures);
    }

    [Fact]
    public void Rule_cards_follow_the_template_and_their_links_work()
    {
        var failures = new List<string>();
        string[] sections = ["Cómo se hace", "Prohibido", "Copiá de", "Lo verifica", "Detalle"];

        foreach (var file in RuleCards())
        {
            var lines = File.ReadAllLines(file);
            if (lines.Length == 0 || !lines[0].StartsWith("# ", StringComparison.Ordinal))
            {
                failures.Add($"{Relative(file)}: falta el título '# <Tema>'.");
            }

            if (!lines.Any(line => line.StartsWith("**Regla:**", StringComparison.Ordinal)))
            {
                failures.Add($"{Relative(file)}: falta '**Regla:**'.");
            }

            var actualSections = lines.Where(line => line.StartsWith("## ", StringComparison.Ordinal))
                .Select(line => line[3..]).ToArray();
            if (!actualSections.SequenceEqual(sections, StringComparer.Ordinal))
            {
                failures.Add($"{Relative(file)}: secciones esperadas: {string.Join(", ", sections)}.");
            }

            failures.AddRange(BrokenMarkdownLinks(file, lines));
            failures.AddRange(BrokenCopyReferences(file, SectionLines(lines, "Copiá de")));
        }

        AssertNoFailures(failures);
    }

    [Fact]
    public void Closed_stage_verification_tests_and_tools_exist()
    {
        var failures = new List<string>();
        var frontExists = Directory.Exists(FrontRoot);
        var testNames = TestNames(Root, frontExists ? FrontRoot : null);
        failures.AddRange(EmptyCSharpTestClasses(Root));

        var backClosedStages = HarnessStage.ClosedStages.ToHashSet();
        var frontClosedStages = frontExists ? FrontClosedStages().ToHashSet() : backClosedStages;
        if (frontExists)
        {
            Assert.Equal(backClosedStages.Order(), frontClosedStages.Order());
        }

        var cards = RuleCards().Select(file => (File: file, Stages: backClosedStages))
            .Concat(frontExists ? RuleCards(FrontRoot).Select(file => (File: file, Stages: frontClosedStages)) : []);
        foreach (var (file, closedStages) in cards)
        {
            foreach (var (line, number) in SectionLinesWithNumbers(File.ReadAllLines(file), "Lo verifica"))
            {
                if (line.Contains("Pendiente:", StringComparison.Ordinal))
                {
                    continue;
                }

                failures.AddRange(MissingVerificationTests(line, number, Relative(file), testNames, closedStages,
                    frontExists));

                if (line.Contains("`BannedSymbols.txt` (E0)", StringComparison.Ordinal)
                    && !File.Exists(Path.Combine(Root, "BannedSymbols.txt")))
                {
                    failures.Add($"{Relative(file)}:{number}: falta BannedSymbols.txt.");
                }

                if (line.Contains("`CA1848` (E0)", StringComparison.Ordinal)
                    && !File.ReadAllText(Path.Combine(Root, ".editorconfig")).Contains("dotnet_diagnostic.CA1848.severity", StringComparison.Ordinal))
                {
                    failures.Add($"{Relative(file)}:{number}: falta CA1848 en .editorconfig.");
                }

                if (line.Contains("CI (E0)", StringComparison.Ordinal)
                    && !File.Exists(Path.Combine(Root, ".github", "workflows", "ci.yml")))
                {
                    failures.Add($"{Relative(file)}:{number}: falta CI.");
                }
            }
        }

        AssertNoFailures(failures);
    }

    [Fact]
    public void Configuration_documentation_names_existing_tests()
    {
        var path = Path.Combine(Root, "docs", "operations", "configuracion.md");
        var names = TestNames(Root, null);
        var missing = Regex.Matches(File.ReadAllText(path), @"\b[A-Za-z][A-Za-z0-9_]*Tests\b",
                RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Where(name => !names.Contains(name))
            .Select(name => $"docs/operations/configuracion.md: falta el test {name}.")
            .ToArray();

        AssertNoFailures(missing);
    }

    private static IEnumerable<string> MissingPointerFiles(string folder)
    {
        var agents = Path.Combine(folder, "AGENTS.md");
        var claude = Path.Combine(folder, "CLAUDE.md");

        if (!File.Exists(agents))
        {
            yield return $"{Relative(folder)}: falta AGENTS.md.";
        }

        if (!File.Exists(claude))
        {
            yield return $"{Relative(folder)}: falta CLAUDE.md.";
        }
        else if (File.ReadAllText(claude).TrimEnd('\r', '\n') != "@AGENTS.md")
        {
            yield return $"{Relative(claude)}: debe contener solo @AGENTS.md.";
        }
    }

    private static void AssertNoFailures(IEnumerable<string> failures)
    {
        var messages = failures.ToArray();
        Assert.True(messages.Length == 0, string.Join(Environment.NewLine, messages));
    }

    private static IEnumerable<string> MappedFolders()
    {
        var lines = SectionLines(File.ReadAllLines(HarnessDocument), "3. Mapa de carpetas → punteros");
        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^\| `(?<folder>[^`]+)` \|", RegexOptions.CultureInvariant);
            if (match.Success)
            {
                foreach (var folder in ExpandMappedFolder(match.Groups["folder"].Value))
                {
                    if (HasWorkFiles(folder))
                    {
                        yield return folder;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> ExpandMappedFolder(string template)
    {
        var parts = template.Trim('/').Split('/');
        IEnumerable<string> folders = [Root];

        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (index == 0 && part is "Domain" or "Application" or "Infrastructure" or "Api")
            {
                folders = [Path.Combine(Root, "src", $"ArquitecturaBaseMultitenant.{part}")];
            }
            else if (index == 0 && part == "tests")
            {
                folders = [Path.Combine(Root, "tests")];
            }
            else if (part.StartsWith('<') || part.Contains('*'))
            {
                var pattern = part.StartsWith('<') ? "*" : part;
                folders = folders.Where(Directory.Exists)
                    .SelectMany(folder => Directory.EnumerateDirectories(folder, pattern))
                    .Where(folder => !IsBuildDirectory(folder))
                    .Where(folder => template != "Domain/<Módulo>/" || ModuleNames().Contains(Path.GetFileName(folder)))
                    .ToArray();
            }
            else
            {
                folders = folders.Select(folder => Path.Combine(folder, part)).ToArray();
            }
        }

        return folders.Where(Directory.Exists);
    }

    private static IEnumerable<string> AreaFolders()
    {
        var features = Path.Combine(Root, "docs", "features");
        if (!Directory.Exists(features))
        {
            return [];
        }

        var names = Directory.EnumerateFiles(features, "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return CodeDirectories()
            .Where(folder => names.Contains(Path.GetFileName(folder))
                || (names.Contains("roles") && Path.GetFileName(folder).Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
            .Where(HasWorkFiles);
    }

    private static IEnumerable<string> FeatureFolders()
    {
        var features = Path.Combine(Root, "docs", "features");
        if (!Directory.Exists(features)) yield break;

        foreach (var file in Directory.EnumerateFiles(features, "*.md"))
        {
            var inFolders = false;
            foreach (var line in File.ReadLines(file))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    inFolders = line == "## Carpetas";
                    continue;
                }

                if (!inFolders) continue;
                var match = Regex.Match(line, @"^\| `(?<path>(?:src|tests)/[^`]+)` \|", RegexOptions.CultureInvariant);
                if (match.Success) yield return Path.Combine(Root,
                    match.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar));
            }
        }
    }

    private static HashSet<string> ModuleNames()
    {
        // WhatsApp es el único módulo nombrado en el mapa actual. Los demás se reconocen
        // por sus carpetas Modules/<Módulo> en las otras capas.
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "WhatsApp" };
        foreach (var layer in new[] { "Application", "Infrastructure", "Api" })
        {
            var modules = Path.Combine(Root, "src", "ArquitecturaBaseMultitenant." + layer, "Modules");
            if (Directory.Exists(modules))
            {
                names.UnionWith(Directory.EnumerateDirectories(modules).Select(folder => Path.GetFileName(folder)!));
            }
        }

        return names;
    }

    private static bool HasWorkFiles(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        return Directory.EnumerateFiles(folder).Any(file => !IsGeneratedFile(file))
            || Directory.EnumerateDirectories(folder)
                .Where(child => !IsBuildDirectory(child))
                .Any(HasWorkFiles);
    }

    private static bool IsGeneratedFile(string file) =>
        Path.GetFileName(file) is ".DS_Store" or "Thumbs.db";

    private static bool IsBuildDirectory(string folder) =>
        Path.GetFileName(folder) is "bin" or "obj" or "node_modules" or "dist";

    private static IEnumerable<string> CodeDirectories() =>
        new[] { Path.Combine(Root, "src"), Path.Combine(Root, "tests") }
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"));

    private static IEnumerable<string> AgentFiles()
    {
        yield return Path.Combine(Root, "AGENTS.md");
        foreach (var folder in new[] { "src", "tests" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, folder), "AGENTS.md", SearchOption.AllDirectories))
            {
                if (!file.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> RuleCards() =>
        RuleCards(Root);

    private static IEnumerable<string> RuleCards(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "docs", "rules"), "*.md")
            .Where(file => Path.GetFileName(file) != "README.md");

    private static IEnumerable<string> BrokenMarkdownLinks(string file, string[] lines)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            foreach (Match match in Regex.Matches(line, @"\[[^\]]+\]\((?<target>[^)]+)\)", RegexOptions.CultureInvariant))
            {
                var reference = match.Groups["target"].Value;
                if (reference.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
                    || reference.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
                    || reference.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var pieces = reference.Split('#', 2);
                var path = pieces[0].Length == 0
                    ? file
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, Uri.UnescapeDataString(pieces[0]).Replace('/', Path.DirectorySeparatorChar)));
                var stageRange = StageRangeForReference(line, match.Index + match.Length);
                if (stageRange is not null && !IsClosedRange(stageRange.Value.First, stageRange.Value.Last,
                    HarnessStage.ClosedStages.ToHashSet()))
                {
                    continue;
                }

                if (!File.Exists(path))
                {
                    if (ExternalCheckoutIsMissing(path))
                    {
                        continue;
                    }

                    yield return $"{Relative(file)}:{index + 1}: enlace roto {reference}.";
                }
                else if (pieces.Length == 2 && !HeadingAnchors(path).Contains(Uri.UnescapeDataString(pieces[1])))
                {
                    yield return $"{Relative(file)}:{index + 1}: sección inexistente {reference}.";
                }
            }
        }
    }

    private static HashSet<string> HeadingAnchors(string file)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(file))
        {
            var match = Regex.Match(line, @"^#{1,6}\s+(?<title>.+?)\s*#*\s*$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var slug = MarkdownSlug(match.Groups["title"].Value);
            counts.TryGetValue(slug, out var count);
            anchors.Add(count == 0 ? slug : $"{slug}-{count}");
            counts[slug] = count + 1;
        }

        return anchors;
    }

    private static string MarkdownSlug(string heading)
    {
        var builder = new StringBuilder();
        foreach (var character in heading.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '-')
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character))
            {
                builder.Append('-');
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> BrokenCopyReferences(string file, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            foreach (Match match in Regex.Matches(line, @"`(?<path>[^`]+)`", RegexOptions.CultureInvariant))
            {
                var reference = match.Groups["path"].Value;
                if (!reference.Contains('/') && !reference.EndsWith(".cs", StringComparison.Ordinal)
                    && !reference.EndsWith(".resx", StringComparison.Ordinal))
                {
                    continue;
                }

                var stageRange = StageRangeForReference(line, match.Index + match.Length);
                if (stageRange is not null && !IsClosedRange(stageRange.Value.First, stageRange.Value.Last,
                    HarnessStage.ClosedStages.ToHashSet()))
                {
                    continue;
                }

                if (!ModelReferenceExists(reference))
                {
                    yield return $"{Relative(file)}: falta el archivo modelo {reference}.";
                }
            }
        }
    }

    private static bool ModelReferenceExists(string reference)
    {
        var path = reference.Replace('/', Path.DirectorySeparatorChar);
        if (path.StartsWith("docs" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return Exists(Path.Combine(Root, path));
        }

        if (path.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            var external = Path.GetFullPath(Path.Combine(Root, path));
            return ExternalCheckoutIsMissing(external) || Exists(external);
        }

        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Api" })
        {
            if (path.StartsWith(layer + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return Exists(Path.Combine(Root, "src", "ArquitecturaBaseMultitenant." + layer, path[(layer.Length + 1)..]));
            }
        }

        var normalized = reference.Replace('\\', '/');
        return Directory.EnumerateFiles(Path.Combine(Root, "src"), "*", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*", SearchOption.AllDirectories))
                .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
                .Any(file => file.Replace('\\', '/').EndsWith(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Exists(string path)
    {
        if (!path.Contains('*') && !path.Contains('?'))
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        var folder = Path.GetDirectoryName(path);
        return folder is not null && Directory.Exists(folder)
            && Directory.EnumerateFileSystemEntries(folder, Path.GetFileName(path)).Any();
    }

    private static bool ExternalCheckoutIsMissing(string path)
    {
        var relative = Path.GetRelativePath(Root, path);
        if (!relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        var sibling = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[1];
        var known = sibling is "ArquitecturaBase" or "ArquitecturaBaseFront" or "ArquitecturaBaseMutitenantFront";
        return known && !Directory.Exists(Path.Combine(Directory.GetParent(Root)!.FullName, sibling));
    }

    private sealed record CSharpTestClass(string Name, string File, bool HasTestMethod, bool IsPartial, string Identity);

    private sealed record CSharpScope(string Name, int Open, int Close);

    private sealed record CSharpType(string Name, string Identity, int Open, int Close, bool IsClass, bool IsPartial);

    private static HashSet<string> TestNames(string backendRoot, string? frontendRoot)
    {
        var names = CSharpTestClasses(backendRoot)
            .Where(testClass => testClass.HasTestMethod)
            .Select(testClass => testClass.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var folder in new[] { Path.Combine(backendRoot, "scripts"), Path.Combine(backendRoot, "tests") })
        {
            names.UnionWith(WorkFiles(folder).Where(IsScriptTest).Select(Path.GetFileName)!);
        }
        if (frontendRoot is not null)
        {
            foreach (var folder in new[] { Path.Combine(frontendRoot, "src"), Path.Combine(frontendRoot, "tests"), Path.Combine(frontendRoot, "scripts") })
            {
                names.UnionWith(WorkFiles(folder).Where(IsScriptTest).Select(Path.GetFileName)!);
            }
        }

        return names;
    }

    private static bool IsScriptTest(string file) =>
        file.EndsWith(".test.mjs", StringComparison.Ordinal)
        || file.EndsWith(".test.ts", StringComparison.Ordinal)
        || file.EndsWith(".test.tsx", StringComparison.Ordinal);

    private static IEnumerable<string> WorkFiles(string folder)
    {
        if (!Directory.Exists(folder)) yield break;
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(current)) yield return file;
            foreach (var child in Directory.EnumerateDirectories(current).Where(child => !IsBuildDirectory(child))) pending.Push(child);
        }
    }

    private static IEnumerable<CSharpTestClass> CSharpTestClasses(string backendRoot)
    {
        var testsRoot = Path.Combine(backendRoot, "tests");
        var declarations = new List<CSharpTestClass>();
        foreach (var file in WorkFiles(testsRoot).Where(file => file.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var code = MaskNonCode(File.ReadAllText(file));
            var namespaces = CSharpNamespaces(code).ToArray();
            var types = CSharpTypes(code, file).ToArray();
            var project = CSharpProject(file, testsRoot);
            foreach (var type in types.Where(type => type.IsClass && type.Name.EndsWith("Tests", StringComparison.Ordinal)))
            {
                var namespaceName = string.Join('.', namespaces
                    .Where(scope => scope.Open < type.Open && scope.Close > type.Close).Select(scope => scope.Name));
                var enclosingTypes = types.Where(parent => parent.Open < type.Open && parent.Close > type.Close)
                    .Select(parent => parent.Identity);
                var identity = project + "|" + namespaceName + "|" + string.Join('+', enclosingTypes.Append(type.Identity));
                declarations.Add(new CSharpTestClass(type.Name, file, HasTestMethod(code, type.Open, type.Close), type.IsPartial, identity));
            }
        }

        var testedPartialTypes = declarations.Where(type => type.IsPartial && type.HasTestMethod)
            .Select(type => type.Identity).ToHashSet(StringComparer.Ordinal);
        return declarations.Select(type => type with
        {
            HasTestMethod = type.HasTestMethod || type.IsPartial && testedPartialTypes.Contains(type.Identity)
        });
    }

    private static string CSharpProject(string file, string testsRoot)
    {
        for (var folder = Path.GetDirectoryName(file); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            var projects = Directory.EnumerateFiles(folder, "*.csproj").ToArray();
            if (projects.Length == 1) return projects[0];
            // Sin un dueño inequívoco no se unen declaraciones entre archivos.
            if (projects.Length > 1) return file;
            if (folder == testsRoot) break;
        }
        return testsRoot;
    }

    private static IEnumerable<CSharpScope> CSharpNamespaces(string code)
    {
        const string pattern = @"\bnamespace\s+(?<name>@?[A-Za-z_][A-Za-z0-9_]*(?:\s*\.\s*@?[A-Za-z_][A-Za-z0-9_]*)*)\s*(?<delimiter>[;{])";
        foreach (Match match in Regex.Matches(code, pattern, RegexOptions.CultureInvariant))
        {
            var open = match.Index + match.Length - 1;
            var close = code[open] == ';' ? code.Length : ClosingBrace(code, open);
            if (close < 0) continue;
            var name = Regex.Replace(match.Groups["name"].Value, @"\s|@", "", RegexOptions.CultureInvariant);
            yield return new CSharpScope(name, open, close);
        }
    }

    private static IEnumerable<CSharpType> CSharpTypes(string code, string file)
    {
        const string pattern = @"\b(?<modifiers>(?:(?:public|internal|protected|private|static|abstract|sealed|new|unsafe|readonly|ref|file|partial)\s+)*)(?<kind>record(?:\s+(?:class|struct))?|class|struct|interface)\s+@?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*<(?<parameters>[^<>]+)>)?[^{};]*\{";
        foreach (Match match in Regex.Matches(code, pattern, RegexOptions.CultureInvariant))
        {
            var open = match.Index + match.Length - 1;
            var close = ClosingBrace(code, open);
            if (close < 0) continue;
            var name = match.Groups["name"].Value;
            var kind = Regex.Replace(match.Groups["kind"].Value, @"\s+", " ", RegexOptions.CultureInvariant);
            var isClass = kind is "class" or "record class";
            if (kind == "record") kind = "record class";
            var modifiers = match.Groups["modifiers"].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var partial = modifiers.Contains("partial", StringComparer.Ordinal);
            var arity = match.Groups["parameters"].Success ? match.Groups["parameters"].Value.Count(character => character == ',') + 1 : 0;
            var identity = kind + ":" + name + "`" + arity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Un contenedor no partial o file-local no identifica el mismo tipo en otro archivo.
            if (!partial) identity += "@" + file + ":" + open;
            else if (modifiers.Contains("file", StringComparer.Ordinal)) identity += "@" + file;
            yield return new CSharpType(name, identity, open, close, isClass, partial);
        }
    }

    private static int ClosingBrace(string code, int open)
    {
        var depth = 0;
        for (var index = open; index < code.Length; index++)
        {
            if (code[index] == '{') depth++;
            if (code[index] == '}' && --depth == 0) return index;
        }
        return -1;
    }

    private static bool HasTestMethod(string code, int open, int close)
    {
        var body = code[(open + 1)..close];
        const string pattern = @"(?m)^[ \t]*\[(?:Fact|Theory)(?:Attribute)?(?:\([^\r\n]*\))?\](?:\s*\[[^\]]+\])*\s*(?:(?:public|internal|protected|private|static|async|virtual|override|sealed|new)\s+)*[A-Za-z_][A-Za-z0-9_<>,.?\[\]]*\s+[A-Za-z_][A-Za-z0-9_]*\s*\(";
        foreach (Match match in Regex.Matches(body, pattern, RegexOptions.CultureInvariant))
        {
            var depth = 1;
            for (var index = open + 1; index < open + 1 + match.Index; index++)
            {
                if (code[index] == '{') depth++;
                if (code[index] == '}') depth--;
            }
            if (depth == 1) return true;
        }
        return false;
    }

    private static string MaskNonCode(string source)
    {
        var masked = source.ToCharArray();
        for (var index = 0; index < source.Length;)
        {
            var start = index;
            if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '/')
            {
                index = source.IndexOf('\n', index + 2);
                if (index < 0) index = source.Length;
            }
            else if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? source.Length : end + 2;
            }
            else if (source[index] == '"')
            {
                var quoteCount = 1;
                while (index + quoteCount < source.Length && source[index + quoteCount] == '"') quoteCount++;
                if (quoteCount >= 3)
                {
                    index += quoteCount;
                    while (index < source.Length)
                    {
                        var run = 0;
                        while (index + run < source.Length && source[index + run] == '"') run++;
                        if (run >= quoteCount) { index += run; break; }
                        index += Math.Max(1, run);
                    }
                }
                else
                {
                    var verbatim = start > 0 && source[start - 1] == '@';
                    index++;
                    while (index < source.Length)
                    {
                        if (!verbatim && source[index] == '\\') { index += Math.Min(2, source.Length - index); continue; }
                        if (source[index] == '"')
                        {
                            if (verbatim && index + 1 < source.Length && source[index + 1] == '"') { index += 2; continue; }
                            index++;
                            break;
                        }
                        index++;
                    }
                }
            }
            else if (source[index] == '\'')
            {
                index++;
                while (index < source.Length)
                {
                    if (source[index] == '\\') { index += Math.Min(2, source.Length - index); continue; }
                    if (source[index++] == '\'') break;
                }
            }
            else
            {
                index++;
                continue;
            }

            for (var cursor = start; cursor < index; cursor++)
            {
                if (masked[cursor] is not '\r' and not '\n') masked[cursor] = ' ';
            }
        }
        return new string(masked);
    }

    private static IEnumerable<string> EmptyCSharpTestClasses(string backendRoot) =>
        CSharpTestClasses(backendRoot)
            .Where(testClass => !testClass.HasTestMethod)
            .Select(testClass => $"{Path.GetRelativePath(backendRoot, testClass.File).Replace('\\', '/')}: {testClass.Name} no tiene [Fact] ni [Theory] en un método.");

    private static IEnumerable<string> MissingVerificationTests(string line, int number, string file, HashSet<string> testNames,
        IReadOnlySet<int> closedStages, bool frontExists = true)
    {
        const string pattern = @"\b(?:[A-Za-z][A-Za-z0-9_]*Tests|[A-Za-z][A-Za-z0-9_.-]*\.test\.(?:mjs|tsx?))\b";
        foreach (Match match in Regex.Matches(line, pattern, RegexOptions.CultureInvariant))
        {
            var stageRange = StageRangeForReference(line, match.Index + match.Length);
            if (stageRange is null)
            {
                yield return $"{file}:{number}: {match.Value} no indica (E#).";
            }
            else if (IsClosedRange(stageRange.Value.First, stageRange.Value.Last, closedStages)
                && !testNames.Contains(match.Value)
                && (frontExists || !match.Value.EndsWith(".test.ts", StringComparison.Ordinal)
                    && !match.Value.EndsWith(".test.tsx", StringComparison.Ordinal)))
            {
                yield return $"{file}:{number}: falta el test {match.Value} (E{stageRange.Value.Last}).";
            }
        }
    }

    private static int[] FrontClosedStages()
    {
        var path = Path.Combine(FrontRoot, "src", "test", "HarnessStage.ts");
        var match = Regex.Match(File.ReadAllText(path), @"ClosedStages\s*=\s*\[(?<stages>[^\]]*)\]", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"No se pudo leer ClosedStages en {Relative(path)}.");
        return Regex.Matches(match.Groups["stages"].Value, @"\d+", RegexOptions.CultureInvariant)
            .Select(value => int.Parse(value.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    }

    private static (int First, int Last)? StageRangeForReference(string line, int end)
    {
        var pattern = @"\bE(?<first>\d+)(?:\s*[–-]\s*E?(?<last>\d+))?\b";
        var after = Regex.Match(line[end..], pattern, RegexOptions.CultureInvariant);
        if (after.Success)
        {
            return ParseStageRange(after);
        }

        var before = Regex.Matches(line[..end], pattern, RegexOptions.CultureInvariant).LastOrDefault();
        return before is null ? null : ParseStageRange(before);
    }

    private static (int First, int Last) ParseStageRange(Match match) =>
        (int.Parse(match.Groups["first"].Value, System.Globalization.CultureInfo.InvariantCulture),
            match.Groups["last"].Success
                ? int.Parse(match.Groups["last"].Value, System.Globalization.CultureInfo.InvariantCulture)
                : int.Parse(match.Groups["first"].Value, System.Globalization.CultureInfo.InvariantCulture));

    private static bool IsClosedRange(int first, int last, IReadOnlySet<int> closedStages) =>
        first <= last && Enumerable.Range(first, last - first + 1).All(closedStages.Contains);

    private static IEnumerable<string> SectionLines(string[] lines, string heading) =>
        SectionLinesWithNumbers(lines, heading).Select(entry => entry.Line);

    private static IEnumerable<(string Line, int Number)> SectionLinesWithNumbers(string[] lines, string heading)
    {
        var start = Array.FindIndex(lines, line => line == "## " + heading);
        if (start < 0)
        {
            return [];
        }

        return lines.Skip(start + 1).Select((line, index) => (Line: line, Number: start + index + 2))
            .TakeWhile(entry => !entry.Line.StartsWith("## ", StringComparison.Ordinal));
    }

    private static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');
}
