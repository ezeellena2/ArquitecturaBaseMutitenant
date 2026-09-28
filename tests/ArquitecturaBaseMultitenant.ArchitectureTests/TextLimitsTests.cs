using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class TextLimitsTests
{
    [Fact]
    public void Text_lengths_are_declared_once_in_domain()
    {
        Assert.Equal(100, TextLimits.PersonName);
        Assert.Equal(120, TextLimits.OrganizationName);
        Assert.Equal(60, TextLimits.ShortName);
        Assert.Equal(500, TextLimits.Description);
        Assert.Equal(4000, TextLimits.LongText);
    }

    [Fact]
    public void Validation_and_persistence_do_not_repeat_text_length_numbers()
    {
        var sourceRoot = Path.Combine(SolutionRoot.FullPath, "src");
        var violations = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(sourceRoot, file)
                .Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin" or "Migrations"))
            .Where(file => LiteralLengthCall().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(SolutionRoot.FullPath, file));

        Assert.Empty(violations);
    }

    [GeneratedRegex(@"\b(?:HasMaxLength|MaxLength)\s*\(\s*\d", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralLengthCall();
}
