namespace ArquitecturaBaseMultitenant.Application.Configuration.Auth;

/// <summary>Centraliza la clave de sesión del navegador que vincula autenticación y revocación de tokens.</summary>
public static class BrowserSessionKeys
{
    public const string CookieSessionId = "mt_session_id";
}
