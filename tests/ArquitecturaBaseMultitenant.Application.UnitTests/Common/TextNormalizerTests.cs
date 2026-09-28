using ArquitecturaBaseMultitenant.Application.Common.Text;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class TextNormalizerTests
{
    [Fact]
    public void Clean_trims_normalizes_nfc_and_removes_invisible_characters()
    {
        Assert.Equal("José", TextNormalizer.Clean("  Jose\u0301\u200B\u0007  "));
        Assert.Equal("Nombre", TextNormalizer.Clean("\uFEFF\u2060Nombre\u200D"));
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
