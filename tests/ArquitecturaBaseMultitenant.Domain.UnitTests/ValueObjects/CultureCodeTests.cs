using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.ValueObjects;

/// <summary>
/// Comprueba el formato y la igualdad canónica de códigos de cultura. Distingue validar la forma de
/// consultar si una cultura está en el catálogo.
/// </summary>
public sealed class CultureCodeTests
{
    [Theory]
    [InlineData("es-AR", "es-AR")]
    [InlineData("EN-us", "en-US")]
    [InlineData("zh-hant-tw", "zh-Hant-TW")]
    [InlineData("es-419", "es-419")]
    public void Create_accepts_a_bcp47_language_region_shape_without_checking_the_catalog(string input, string expected)
    {
        var result = CultureCode.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("es")]
    [InlineData("es_AR")]
    [InlineData("es-AR ")]
    [InlineData("9s-AR")]
    [InlineData("es-1234")]
    [InlineData("es-X-AR")]
    [InlineData("és-AR")]
    public void Create_rejects_invalid_language_region_shapes(string? input)
    {
        var result = CultureCode.Create(input);

        Assert.True(result.IsFailure);
        Assert.Equal(CultureCodeErrors.Invalid, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Equality_uses_the_canonical_tag()
    {
        Assert.Equal(CultureCode.Create("EN-us").Value, CultureCode.Create("en-US").Value);
        Assert.NotEqual(CultureCode.Create("en-US").Value, CultureCode.Create("es-AR").Value);
    }
}
