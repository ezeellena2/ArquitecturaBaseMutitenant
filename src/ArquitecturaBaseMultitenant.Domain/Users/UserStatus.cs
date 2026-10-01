namespace ArquitecturaBaseMultitenant.Domain.Users;

/// <summary>
/// Describe el ciclo de vida de una cuenta: activa, suspendida, con baja pendiente o eliminada. Permite
/// decidir si puede ingresar y qué acciones siguen disponibles.
/// </summary>
public enum UserStatus
{
    Active,
    Suspended,
    PendingDeletion,
    Deleted,
}
