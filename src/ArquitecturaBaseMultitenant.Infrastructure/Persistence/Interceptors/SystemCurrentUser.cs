using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>Actor técnico E2; la Api leerá los claims en E3.</summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public Access? Access => null;
}
