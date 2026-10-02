using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

/// <summary>Fila proyectada para consultar un rol de la organización activa.</summary>
public sealed record RoleRow(Guid Id, string Name, string? Description, RoleScope Scope, Guid? CompanyId,
    bool IsSystemRole, IReadOnlyList<string> Permissions, uint Version, DateTime CreatedAtUtc);
