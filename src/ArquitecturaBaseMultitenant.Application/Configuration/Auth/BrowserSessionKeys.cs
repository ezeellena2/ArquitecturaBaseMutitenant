using System.Globalization;
using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Auth;

/// <summary>Centraliza la clave de sesión del navegador que vincula autenticación y revocación de tokens.</summary>
public static class BrowserSessionKeys
{
    public const string CookieSessionId = "mt_session_id";
    public const string StartedAtUtc = "session_started_at";

    public static string FormatStartedAtUtc(DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The session instant must be UTC.", nameof(instantUtc));
        return JsonSerializer.Serialize(instantUtc).Trim('"');
    }

    public static DateTime? ReadStartedAtUtc(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instantUtc)
        && instantUtc.Kind == DateTimeKind.Utc ? instantUtc : null;
}
