namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

public sealed record PermissionGroupResponse(string Area, string Name, IReadOnlyList<PermissionItem> Permissions);

/// <summary>
/// Un permiso del catálogo: <c>Includes</c> son los códigos que incluye, de su misma área y su mismo catálogo (decisión 260),
/// y <c>Grantable</c> dice si quien pide lo tiene, para dárselo a otro (decisión 269 y ADR 0061).
/// </summary>
public sealed record PermissionItem(string Code, string Name, string Description, IReadOnlyList<string> Includes, bool Grantable);
