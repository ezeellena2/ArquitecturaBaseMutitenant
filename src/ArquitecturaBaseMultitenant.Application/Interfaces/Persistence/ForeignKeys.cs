namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Nombres de las claves foráneas que la base hace cumplir y que un caso de uso traduce, como <see cref="UniqueKeys"/>.
/// Infrastructure los usa en <c>HasConstraintName</c> y el servicio los compara con
/// <c>ForeignKeyViolationException.ConstraintName</c> para dar su error propio. El control previo del servicio no
/// alcanza ante dos pedidos simultáneos: la clave es la barrera y su choque nunca llega como 500.
/// </summary>
public static class ForeignKeys
{
    /// <summary>
    /// Una asignación apunta a un rol de su organización. Si alguien asigna el rol mientras otro lo borra, choca
    /// el que guarda segundo: la asignación, porque el rol ya no existe, o el borrado, porque el rol tiene personas.
    /// </summary>
    public const string RoleAssignmentRole = "FK_RoleAssignments_Roles_TenantId_RoleId";
}
