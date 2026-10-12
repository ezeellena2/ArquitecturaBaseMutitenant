using System.Globalization;

namespace ArquitecturaBaseMultitenant.Application.Common.Formatting;

/// <summary>Serializa números técnicos de frames, digests y referencias; nunca presenta texto de usuario.</summary>
public static class InvariantWireFormatter
{
    public static string FormatInteger(long value) => value.ToString(CultureInfo.InvariantCulture);
    public static string FormatCanonicalDecimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
