namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>
/// Describe el ciclo de vida de un espacio personal u organización, desde su preparación hasta el cierre.
/// Las reglas de acceso consultan este estado antes de permitir operaciones.
/// </summary>
public enum TenantStatus
{
    PendingApproval,
    Provisioning,
    Active,
    Suspended,
    Closed,
}
