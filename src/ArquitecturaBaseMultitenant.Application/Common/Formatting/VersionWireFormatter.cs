using System.Globalization;

namespace ArquitecturaBaseMultitenant.Application.Common.Formatting;

/// <summary>Serializa versiones y números técnicos invariantes para URLs, frames y referencias.</summary>
public static class VersionWireFormatter
{
    public static string Format(uint version) => version.ToString(CultureInfo.InvariantCulture);
    public static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);
    public static string FormatCanonicalDecimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
