namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>
/// Claves del resumen de cambios de un <see cref="AuditEntry"/> que no son una propiedad de la entidad: las escribe
/// el rastro de auditoría y las muestra la lectura de la auditoría de la organización.
/// </summary>
public static class AuditChangeKeys
{
    /// <summary>Los roles de una persona en toda la organización, por nombre, antes y después.</summary>
    public const string OrganizationRoles = "OrganizationRoles";

    /// <summary>Los roles de una persona en una empresa, por nombre, antes y después.</summary>
    public const string CompanyRoles = "CompanyRoles";
}
