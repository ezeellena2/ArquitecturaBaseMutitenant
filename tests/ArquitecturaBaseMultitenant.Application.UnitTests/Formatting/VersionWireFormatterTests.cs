using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Formatting;

public sealed class VersionWireFormatterTests
{
    [Theory]
    [InlineData("es-AR")]
    [InlineData("en-US")]
    [InlineData("ar-SA")]
    public void A_wire_version_has_ascii_digits_without_localized_separators(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (var version in new uint[] { 0, 1, 10_000, uint.MaxValue })
            {
                var encoded = VersionWireFormatter.Format(version);
                Assert.All(encoded, digit => Assert.InRange(digit, '0', '9'));
                Assert.Equal(version, uint.Parse(encoded, NumberStyles.None, CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
