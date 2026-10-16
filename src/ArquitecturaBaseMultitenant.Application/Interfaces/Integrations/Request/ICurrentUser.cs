using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Identidad firmada de la petición o actor confiable de background; sin ninguno expone valores nulos.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Access? Access { get; }

    DateTime? SessionStartedAtUtc => null;

    /// <summary>La sesión de Plataforma del token (E9), que liga la prueba reciente; null fuera de Plataforma.</summary>
    Guid? PlatformSessionId => null;
}
