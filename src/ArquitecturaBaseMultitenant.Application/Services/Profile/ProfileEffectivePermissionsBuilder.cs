using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

/// <summary>Compone los permisos efectivos de la organización, empresas o Plataforma según el acceso activo.</summary>
internal sealed class ProfileEffectivePermissionsBuilder(IPermissionService permissions,
    IPlatformPermissionService platformPermissions)
{
    internal async Task<EffectivePermissions> BuildAsync(Guid userId, Access access, bool hasActiveMembership,
        CancellationToken cancellationToken)
    {
        if (access == Access.Platform)
            return new EffectivePermissions([], new Dictionary<Guid, IReadOnlyList<string>>(),
                await platformPermissions.GetPermissionsAsync(userId, cancellationToken));

        if (access != Access.Business || !hasActiveMembership) return EffectivePermissions.Empty;

        return new EffectivePermissions(
            await permissions.GetOrganizationPermissionsAsync(userId, cancellationToken),
            await permissions.GetPermissionsByCompanyAsync(userId, cancellationToken), []);
    }
}
