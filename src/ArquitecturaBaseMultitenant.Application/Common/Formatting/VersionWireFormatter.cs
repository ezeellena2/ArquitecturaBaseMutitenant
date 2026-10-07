using System.Globalization;

namespace ArquitecturaBaseMultitenant.Application.Common.Formatting;

/// <summary>Serializa una versión como dígitos invariantes para URLs y validadores HTTP.</summary>
public static class VersionWireFormatter
{
    public static string Format(uint version) => version.ToString(CultureInfo.InvariantCulture);
}
