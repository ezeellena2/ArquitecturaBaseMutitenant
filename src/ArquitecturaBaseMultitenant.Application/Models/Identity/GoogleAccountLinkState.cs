namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Propiedades del protocolo protegidas por el middleware OAuth, nunca recibidas del callback query.</summary>
public static class GoogleAccountLinkState
{
    public const string UserIdKey = "AccountLinkUserId";
    public const string CallbackPath = "/api/auth/external/callback";
}
