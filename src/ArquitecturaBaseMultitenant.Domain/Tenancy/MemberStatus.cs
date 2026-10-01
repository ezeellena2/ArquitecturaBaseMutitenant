namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>
/// Indica si una membresía está invitada, activa, inactiva o retirada. Las reglas lo usan para decidir si
/// la persona puede operar en ese espacio.
/// </summary>
public enum MemberStatus
{
    Invited,
    Active,
    Inactive,
    Removed,
}
