using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Identidad firmada de la petición o actor confiable de background; sin ninguno expone valores nulos.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Access? Access { get; }

    DateTime? SessionStartedAtUtc => null;
}
