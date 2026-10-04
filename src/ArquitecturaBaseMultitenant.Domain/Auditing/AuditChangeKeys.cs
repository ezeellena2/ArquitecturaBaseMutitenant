namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>
/// Claves y valores del resumen de cambios de un <see cref="AuditEntry"/> que no son una propiedad de la entidad: los
/// escribe el rastro de auditoría y los muestra la lectura de la auditoría de la organización.
/// </summary>
public static class AuditChangeKeys
{
    /// <summary>
    /// El estado «vencida» de una invitación. No se guarda en la invitación, porque sale de la fecha: lo escribe el
    /// registro de su vencimiento y la lectura lo muestra como «Invitación vencida».
    /// </summary>
    public const string ExpiredInvitationStatus = "Expired";

    /// <summary>Los roles de una persona en toda la organización, por nombre, antes y después.</summary>
    public const string OrganizationRoles = "OrganizationRoles";

    /// <summary>Los roles de una persona en una empresa, por nombre, antes y después.</summary>
    public const string CompanyRoles = "CompanyRoles";

    /// <summary>Las empresas de una invitación, por su identificador: la lectura las muestra por nombre.</summary>
    public const string CompanyIds = "CompanyIds";
}
