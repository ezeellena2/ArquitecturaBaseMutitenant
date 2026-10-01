namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>
/// Clasifica la operación registrada en el rastro de auditoría: creación, edición, eliminación,
/// restauración o acción específica. Facilita interpretar qué cambió.
/// </summary>
public enum AuditAction
{
    Created,
    Updated,
    Deleted,
    Restored,
    Custom,
}
