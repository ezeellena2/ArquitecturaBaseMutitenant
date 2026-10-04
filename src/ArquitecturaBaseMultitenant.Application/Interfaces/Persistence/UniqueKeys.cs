namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Nombres de los índices únicos que la base hace cumplir. Infrastructure los usa en <c>HasDatabaseName</c> y el caso
/// de uso los compara con <c>UniqueConstraintViolationException.ConstraintName</c> para dar su error propio.
/// </summary>
public static class UniqueKeys
{
    /// <summary>
    /// El nombre de un rol es único dentro de su empresa; los roles sin empresa (los de toda la organización y los
    /// que valen en cada empresa) comparten un solo espacio de nombres (<c>NULLS NOT DISTINCT</c>).
    /// </summary>
    public const string RoleName = "IX_Roles_TenantId_CompanyId_NormalizedName";
}
