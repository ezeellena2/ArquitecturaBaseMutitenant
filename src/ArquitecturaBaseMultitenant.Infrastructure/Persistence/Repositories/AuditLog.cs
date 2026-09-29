using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Agrega un evento explícito al mismo ChangeTracker que guardará el UnitOfWork.</summary>
internal sealed class AuditLog(
    ApplicationDbContext context,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IAuditLog
{
    public void Record(AuditAction action, string entityType, Guid entityId, IReadOnlyDictionary<string, object?> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var (actorKind, actorId) = AuditActorResolver.Resolve(currentUser);
        context.Add(AuditEntry.Create(tenantContext.RequiredTenantId, actorKind, actorId,
            action, entityType, entityId, JsonSerializer.Serialize(data), timeProvider.GetUtcNow().UtcDateTime));
    }
}
