using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Registra un evento explícito en la misma transacción que el caso de uso.</summary>
public interface IAuditLog
{
    /// <summary>
    /// Agrega un evento que no surge de un cambio de entidad. Los datos sensibles de <paramref name="data"/>
    /// deben llegar enmascarados; Infrastructure los serializa para AuditEntry.
    /// </summary>
    void Record(AuditAction action, string entityType, Guid entityId, IReadOnlyDictionary<string, object?> data);
}
