namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

public sealed record PermissionGroupResponse(string Area, string Name, IReadOnlyList<PermissionItem> Permissions);

public sealed record PermissionItem(string Code, string Name, string Description);
