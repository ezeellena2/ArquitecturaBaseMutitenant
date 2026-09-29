using System.Globalization;

namespace ArquitecturaBaseMultitenant.Api.ErrorHandling;

/// <summary>Convierte segundos enteros al valor invariante del header HTTP Retry-After.</summary>
internal static class RetryAfterHeaderFormatter
{
    public static string Format(int seconds) => seconds.ToString(CultureInfo.InvariantCulture);
}
