using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Un dato global auditable exige evidencia de seguridad dentro de la misma transacción.</summary>
public interface IGlobalSecurityAudited : IAuditable
{
    SecurityEventType SecurityEventType { get; }
    Guid? SecurityEventTargetTenantId { get; }
}
