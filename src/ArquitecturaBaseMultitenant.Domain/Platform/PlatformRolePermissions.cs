using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

/// <summary>Permisos fijos de cada rol de Plataforma; solo se asignan claves del catálogo.</summary>
public static class PlatformRolePermissions
{
    private static readonly IReadOnlyCollection<string> Owner = Array.AsReadOnly(PlatformPermissions.All.ToArray());
    private static readonly IReadOnlyCollection<string> Support = Array.AsReadOnly(new[]
    {
        PlatformPermissions.Tenants.Read,
        PlatformPermissions.Accounts.Read,
        PlatformPermissions.Recoveries.Manage,
        PlatformPermissions.Audit.Read,
    });

    public static IReadOnlyCollection<string> For(PlatformRole role) => role switch
    {
        PlatformRole.Owner => Owner,
        PlatformRole.Support => Support,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
