namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>
/// Distingue si una acción auditada provino de un usuario, un operador de plataforma o el sistema. Conserva
/// el origen aun cuando no haya una persona en sesión.
/// </summary>
public enum AuditActorKind
{
    User,
    PlatformOperator,
    System,
}
