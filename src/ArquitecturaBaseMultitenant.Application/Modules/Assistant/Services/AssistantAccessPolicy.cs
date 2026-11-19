using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Rechecks the current actor, live gate and effective permissions on every internal execution.</summary>
internal sealed class AssistantAccessPolicy(ICurrentUser user, AssistantSessionPolicy session, IFeatureService features,
    IPermissionService organization, IPlatformPermissionService platform, IAssistantNoticeReader notices)
{
    internal Access? CurrentAccess => user.Access;

    // Aceptar el primer aviso requiere CanUseAsync. Sólo la vía de datos/ejecución exige consentimiento vigente.
    internal async Task<bool> CanExecuteAsync(Access? access, IReadOnlyList<string> permissions,
        Guid? companyId, CancellationToken ct)
    {
        if (!await CanUseAsync(access, permissions, companyId, ct) || user.UserId is not { } owner) return false;
        var version = notices.CurrentVersion;
        return await notices.ExistsAsync(owner, version, ct)
            && string.Equals(version, notices.CurrentVersion, StringComparison.Ordinal);
    }

    internal async Task<bool> CanUseAsync(Access? access, IReadOnlyList<string> permissions,
        Guid? companyId, CancellationToken ct)
    {
        if (user.UserId is not { } id || user.Access is not { } current || access is { } requested && requested != current)
            return false;
        if (!await session.IsActiveAsync(ct)) return false;
        if (!await features.IsEnabledAsync("assistant", ct)) return false;
        if (current == Access.Business) return await CanUseInBusinessAsync(id, permissions, companyId, ct);
        if (current == Access.Platform && !await platform.HasPermissionAsync(id, PlatformPermissions.Assistant.Use, ct)) return false;
        // En Personal ningún permiso de herramienta alcanza; en Plataforma, cada uno se pregunta a sus permisos.
        foreach (var permission in permissions)
            if (current != Access.Platform || !await platform.HasPermissionAsync(id, permission, ct)) return false;
        return true;
    }

    // La puerta y cada permiso se comparan contra una sola lectura de cada proyección (base de la 12, permisos.md), con las
    // mismas respuestas que HasPermissionAsync y HasCompanyPermissionAsync.
    private async Task<bool> CanUseInBusinessAsync(Guid id, IReadOnlyList<string> permissions, Guid? companyId,
        CancellationToken ct)
    {
        var granted = await organization.GetBusinessPermissionsAsync(id, ct);
        if (!granted.Organization.Contains(Permissions.Assistant.Use, StringComparer.Ordinal)) return false;
        foreach (var permission in permissions)
        {
            var allowed = !permission.StartsWith("company.", StringComparison.Ordinal)
                ? granted.Organization.Contains(permission, StringComparer.Ordinal)
                : companyId is { } company
                    ? await organization.HasCompanyPermissionAsync(granted, company, permission, ct)
                    : granted.Organization.Contains(permission, StringComparer.Ordinal)
                        || granted.ByCompany.Values.Any(values => values.Contains(permission, StringComparer.Ordinal));
            if (!allowed) return false;
        }
        return true;
    }
}
