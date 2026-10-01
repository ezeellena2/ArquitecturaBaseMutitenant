using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.ValueObjects;

/// <summary>
/// Comprueba el formato de tres letras y la igualdad canónica de códigos de moneda. La pertenencia al
/// catálogo se valida fuera del objeto de valor.
/// </summary>
public sealed class CurrencyCodeTests
{
    [Theory]
    [InlineData("ARS", "ARS")]
    [InlineData("usd", "USD")]
    [InlineData("ZZZ", "ZZZ")]
    public void Create_accepts_three_ascii_letters_without_checking_the_catalog(string input, string expected)
    {
        var result = CurrencyCode.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ARS")]
    [InlineData("AR ")]
    [InlineData("AR")]
    [InlineData("ABCD")]
    [InlineData("A1S")]
    [InlineData("A-S")]
    [InlineData("ÁRS")]
    public void Create_rejects_codes_that_are_not_alpha_three(string? input)
    {
        var result = CurrencyCode.Create(input);

        Assert.True(result.IsFailure);
        Assert.Equal(CurrencyCodeErrors.Invalid, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Equality_uses_the_canonical_code()
    {
        var first = CurrencyCode.Create("ars").Value;
        var same = CurrencyCode.Create("ARS").Value;

        Assert.Equal(first, same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, CurrencyCode.Create("USD").Value);
    }
}
