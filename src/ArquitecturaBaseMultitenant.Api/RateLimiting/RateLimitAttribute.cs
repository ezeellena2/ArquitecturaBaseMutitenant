using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Api.RateLimiting;

/// <summary>
/// El perfil de cuota propio de una ruta (E9), que se suma a los generales de la petición: ingreso, invitaciones y los que
/// aporte un módulo con su metadata. Cuenta antes de que la ruta haga su trabajo.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RateLimitAttribute(RateLimitProfile profile) : Attribute
{
    public RateLimitProfile Profile { get; } = profile;
}
