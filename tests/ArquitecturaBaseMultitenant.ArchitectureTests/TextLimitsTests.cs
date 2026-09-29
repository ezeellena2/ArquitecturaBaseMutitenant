using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using FluentValidation;

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

    [Theory]
    [InlineData("MaximumLength(100)")]
    [InlineData("Length(1, 100)")]
    [InlineData("StringLength(100)")]
    [InlineData("HasMaxLength(100)")]
    public void Detector_recognizes_all_literal_length_rules(string source) =>
        Assert.Matches(LiteralLengthCall(), source);

    [Fact]
    public void Person_name_accepts_the_exact_limit_and_rejects_the_next_character()
    {
        var validator = new InlineValidator<TextProbe>();
        validator.RuleFor(probe => probe.Name).PersonName();

        Assert.True(validator.Validate(new TextProbe(new string('a', TextLimits.PersonName))).IsValid);
        Assert.False(validator.Validate(new TextProbe(new string('a', TextLimits.PersonName + 1))).IsValid);
    }

    [GeneratedRegex(@"\b(?:HasMaxLength|MaxLength|MaximumLength|Length|StringLength)\s*\(\s*\d", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralLengthCall();

    private sealed record TextProbe(string? Name);
}
