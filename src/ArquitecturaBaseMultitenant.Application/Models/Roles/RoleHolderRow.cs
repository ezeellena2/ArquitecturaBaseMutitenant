namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

/// <summary>
/// Una persona activa que tiene un rol de organización, con todo lo que le da permisos (B12 de los arreglos de la revisión):
/// sus roles de organización y los de cada empresa activa donde tiene la membresía activa, como los lee la caché de permisos.
/// </summary>
public sealed record RoleHolderRow(Guid UserId, IReadOnlyList<Guid> OrganizationRoleIds,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> CompanyRoleIds);
