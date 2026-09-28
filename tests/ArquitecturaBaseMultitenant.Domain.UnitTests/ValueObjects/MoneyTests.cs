using System.Globalization;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Add_and_subtract_keep_the_currency()
    {
        var currency = CurrencyCode.Create("ARS").Value;
        var left = new Money(10.125m, currency);
        var right = new Money(5.125m, currency);

        Assert.Equal(new Money(15.250m, currency), left.Add(right));
        Assert.Equal(new Money(5.000m, currency), left.Subtract(right));
        Assert.Equal(10.125m, left.Amount);
    }

    [Fact]
    public void Add_and_subtract_reject_different_currencies()
    {
        var pesos = new Money(10m, CurrencyCode.Create("ARS").Value);
        var dollars = new Money(5m, CurrencyCode.Create("USD").Value);

        Assert.Throws<InvalidOperationException>(() => pesos.Add(dollars));
        Assert.Throws<InvalidOperationException>(() => pesos.Subtract(dollars));
    }

    [Theory]
    [InlineData("1.5", 0, "2")]
    [InlineData("-1.5", 0, "-2")]
    [InlineData("1.005", 2, "1.01")]
    [InlineData("-1.005", 2, "-1.01")]
    [InlineData("1.2345", 3, "1.235")]
    [InlineData("-1.2345", 3, "-1.235")]
    public void Round_uses_explicit_minor_units_and_away_from_zero(string input, int minorUnits, string expected)
    {
        var currency = CurrencyCode.Create("ZZZ").Value;
        var value = new Money(decimal.Parse(input, CultureInfo.InvariantCulture), currency);

        var rounded = value.Round(minorUnits);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), rounded.Amount);
        Assert.Equal(currency, rounded.Currency);
        Assert.Equal(decimal.Parse(input, CultureInfo.InvariantCulture), value.Amount);
    }

    [Fact]
    public void Money_comparison_includes_currency()
    {
        var currency = CurrencyCode.Create("ARS").Value;
        var first = new Money(10m, currency);

        Assert.Equal(first, new Money(10m, CurrencyCode.Create("ars").Value));
        Assert.Equal(first.GetHashCode(), new Money(10m, currency).GetHashCode());
        Assert.NotEqual(first, new Money(10m, CurrencyCode.Create("USD").Value));
    }
}
