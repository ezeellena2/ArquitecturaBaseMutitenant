using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>Traduce el usuario y acceso actuales al actor que guarda la auditoría. Si no hay identidad, registra una acción de sistema.</summary>
internal static class AuditActorResolver
{
    public static (AuditActorKind Kind, Guid? Id) Resolve(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        if (currentUser.UserId is not { } userId || userId == Guid.Empty)
        {
            return (AuditActorKind.System, null);
        }

        return (currentUser.Access == Access.Platform
            ? AuditActorKind.PlatformOperator
            : AuditActorKind.User, userId);
    }
}
