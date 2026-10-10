using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>Representa una operación sin usuario autenticado, como el bootstrap de la base. La Api sustituye este valor por el contexto del request cuando hay una sesión.</summary>
internal sealed class SystemCurrentUser(IExecutionActorContext? executionActors = null) : ICurrentUser, IRequestInfo
{
    public Guid? UserId => executionActors?.Actor?.OwnerUserId;

    public Access? Access => executionActors?.Actor?.Access;
    public DateTime? SessionStartedAtUtc => executionActors?.Actor?.SessionStartedAtUtc;
    public string? IpAddress => null;
    public string? UserAgent => null;
}
