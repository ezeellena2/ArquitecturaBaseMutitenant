namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

public sealed record DeleteRoleRequest(Guid RoleId, uint? Version);
