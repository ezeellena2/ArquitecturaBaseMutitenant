using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Identidad de la petición actual; un worker sin sesión expone valores nulos.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Access? Access { get; }
}
