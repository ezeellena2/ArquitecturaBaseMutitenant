namespace ArquitecturaBaseMultitenant.Api.RateLimiting;

/// <summary>
/// Centraliza los nombres de las políticas que limitan pedidos y verificaciones de códigos por IP. Las
/// acciones HTTP usan estas claves para activar la política correspondiente.
/// </summary>
internal static class RateLimitPolicies
{
    public const string LoginCode = "login-code";

    public const string LoginVerify = "login-verify";
}
