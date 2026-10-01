namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>
/// Identifica por qué se retiró una membresía. La baja de la cuenta queda registrada como motivo para
/// conservar la explicación del cambio.
/// </summary>
public enum MemberRemovalReason { AccountDeleted }
