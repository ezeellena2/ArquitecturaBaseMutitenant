using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

internal static class AuditActorResolver
{
    public static (AuditActorKind Kind, Guid? Id) Resolve(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        if (currentUser.UserId is not { } userId || userId == Guid.Empty)
        {
            return (AuditActorKind.System, null);
        }

        return (currentUser.Access == AccessKind.Platform
            ? AuditActorKind.PlatformOperator
            : AuditActorKind.User, userId);
    }
}
