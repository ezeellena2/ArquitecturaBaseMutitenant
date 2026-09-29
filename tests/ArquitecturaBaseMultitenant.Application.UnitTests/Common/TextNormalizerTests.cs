using ArquitecturaBaseMultitenant.Application.Common.Text;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class TextNormalizerTests
{
    [Fact]
    public void Clean_trims_normalizes_nfc_and_removes_invisible_characters()
    {
        Assert.Equal("José", TextNormalizer.Clean("  Jose\u0301\u200B\u0007  "));
        Assert.Equal("Nombre\u200D", TextNormalizer.Clean("\uFEFF\u2060Nombre\u200D"));
    }

    [Fact]
    public void Clean_preserves_joiners_in_legitimate_written_text()
    {
        Assert.Equal("می\u200Cخواهم", TextNormalizer.Clean("می\u200Cخواهم"));
        Assert.Equal("👩\u200D💻", TextNormalizer.Clean("👩\u200D💻"));
        Assert.Equal("می\u200Cخواهم", TextNormalizer.CleanName("می\u200Cخواهم"));
    }

    [Fact]
    public void Clean_removes_only_the_declared_unsafe_invisibles_and_controls()
    {
        Assert.Equal("ab", TextNormalizer.Clean(
            "a\u00AD\u200B\uFEFF\u2060\u061C\u200E\u200F\u202A\u202E\u2066\u2069\u0007b"));
        Assert.Equal("a\u200Cb\u200Dc", TextNormalizer.Clean("a\u200Cb\u200Dc"));
    }

    [Fact]
    public void Clean_preserves_internal_spaces_tabs_and_line_breaks()
    {
        Assert.Equal("Grupo  La Cosecha\nFila\t2", TextNormalizer.Clean(" \u200BGrupo  La Cosecha\r\nFila\t2\u2060 "));
        Assert.Equal("Párrafo  uno\nPárrafo dos", TextNormalizer.Clean(" Párrafo  uno\nPárrafo dos "));
    }

    [Fact]
    public void Clean_returns_null_when_nothing_visible_remains()
    {
        Assert.Null(TextNormalizer.Clean(null));
        Assert.Null(TextNormalizer.Clean(string.Empty));
        Assert.Null(TextNormalizer.Clean(" \u200B\0\t\n\u2060 "));
    }

    [Fact]
    public void Clean_replaces_unpaired_utf16_surrogates_with_replacement_character()
    {
        Assert.Equal("a\uFFFDb", TextNormalizer.Clean("a\uD800b"));
    }

    [Fact]
    public void CleanName_collapses_whitespace_only_for_names()
    {
        const string input = " Grupo  La\tCosecha\nSur ";

        Assert.Equal("Grupo La Cosecha Sur", TextNormalizer.CleanName(input));
        Assert.Equal("Grupo  La\tCosecha\nSur", TextNormalizer.Clean(input));
    }
}
