using System.Text;
using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class HarnessTests
{
    private static readonly string Root = SolutionRoot.FullPath;
    private static readonly string Rules = Path.Combine(Root, "docs", "rules");
    private static readonly string HarnessDocument = Path.Combine(Root, "docs", "architecture", "arnes.md");

    [Fact]
    public void Persistence_stage_is_closed()
    {
        Assert.True(HarnessStage.Closed >= 2);
    }

    [Fact]
    public void Root_and_existing_mapped_folders_have_both_agent_files()
    {
        var folders = new[] { Root }
            .Concat(MappedFolders())
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
        var testNames = TestClassNames();

        foreach (var file in RuleCards())
        {
            foreach (var (line, number) in SectionLinesWithNumbers(File.ReadAllLines(file), "Lo verifica"))
            {
                if (line.Contains("Pendiente:", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in Regex.Matches(line, @"\b[A-Za-z][A-Za-z0-9_]*Tests\b", RegexOptions.CultureInvariant))
                {
                    var stage = StageForReference(line, match.Index + match.Length);
                    if (stage is null)
                    {
                        failures.Add($"{Relative(file)}:{number}: {match.Value} no indica (E#).");
                    }
                    else if (stage <= HarnessStage.Closed && !testNames.Contains(match.Value))
                    {
                        failures.Add($"{Relative(file)}:{number}: falta el test {match.Value} (E{stage}).");
                    }
                }

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
        Directory.EnumerateFiles(Rules, "*.md")
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
                var stage = StageForReference(line, match.Index + match.Length);
                if (stage is not null && stage > HarnessStage.Closed)
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

                var stage = StageForReference(line, match.Index + match.Length);
                if (stage is not null && stage > HarnessStage.Closed)
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

    private static HashSet<string> TestClassNames() =>
        Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\bclass\s+(?<name>[A-Za-z][A-Za-z0-9_]*Tests)\b", RegexOptions.CultureInvariant)
                .Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

    private static int? StageForReference(string line, int end)
    {
        var after = Regex.Match(line[end..], @"\bE(?<stage>\d+)\b", RegexOptions.CultureInvariant);
        if (after.Success)
        {
            return int.Parse(after.Groups["stage"].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        var before = Regex.Matches(line[..end], @"\bE(?<stage>\d+)\b", RegexOptions.CultureInvariant).LastOrDefault();
        return before is null ? null : int.Parse(before.Groups["stage"].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

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
