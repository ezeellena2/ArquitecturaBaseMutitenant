namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

/// <summary>
/// Los permisos de una persona en la organización Business activa, leídos juntos: los de la organización, con los que se
/// proyectan desde una empresa; los de cada empresa donde tiene alguno, con los de la organización incluidos, y los que le
/// dan sus roles de organización, sin proyecciones (los de <c>GetAssignedOrganizationPermissionsAsync</c>).
/// </summary>
public sealed record BusinessPermissions(IReadOnlyList<string> Organization,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> ByCompany, IReadOnlyList<string> Assigned)
{
    /// <summary>La organización donde se leyeron: no valen en otra (null fuera de una organización Business).</summary>
    public Guid? TenantId { get; init; }
}
