using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Formatting;

public sealed class InvariantWireFormatterTests
{
    [Theory]
    [InlineData("es-AR")]
    [InlineData("en-US")]
    [InlineData("ar-SA")]
    public void Technical_numbers_keep_the_same_wire_bytes_and_decimal_scale_does_not_change_a_reference(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("10000", VersionWireFormatter.Format(10_000));
            Assert.Equal("9223372036854775807", VersionWireFormatter.Format(long.MaxValue));
            Assert.Equal("1.23", VersionWireFormatter.FormatCanonicalDecimal(1.230000m));
            Assert.Equal(VersionWireFormatter.FormatCanonicalDecimal(1.23m), VersionWireFormatter.FormatCanonicalDecimal(1.230000m));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
