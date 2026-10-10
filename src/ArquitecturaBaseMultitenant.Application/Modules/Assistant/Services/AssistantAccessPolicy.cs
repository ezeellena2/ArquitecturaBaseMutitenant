using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Rechecks the current actor, live gate and effective permissions on every internal execution.</summary>
internal sealed class AssistantAccessPolicy(ICurrentUser user, AssistantSessionPolicy session, IFeatureService features,
    IPermissionService organization, IPlatformPermissionService platform)
{
    internal Access? CurrentAccess => user.Access;

    internal async Task<bool> CanUseAsync(Access? access, IReadOnlyList<string> permissions,
        Guid? companyId, CancellationToken ct)
    {
        if (user.UserId is not { } id || user.Access is not { } current || access is { } requested && requested != current)
            return false;
        if (!await session.IsActiveAsync(ct)) return false;
        if (!await features.IsEnabledAsync("assistant", ct)) return false;
        if (current == Access.Business && !await organization.HasPermissionAsync(id, Permissions.Assistant.Use, ct)) return false;
        if (current == Access.Platform && !await platform.HasPermissionAsync(id, PlatformPermissions.Assistant.Use, ct)) return false;
        foreach (var permission in permissions)
        {
            var allowed = current switch
            {
                Access.Platform => await platform.HasPermissionAsync(id, permission, ct),
                Access.Business when permission.StartsWith("company.", StringComparison.Ordinal) && companyId is { } company =>
                    await organization.HasCompanyPermissionAsync(id, company, permission, ct),
                Access.Business when permission.StartsWith("company.", StringComparison.Ordinal) =>
                    await organization.HasPermissionAsync(id, permission, ct)
                    || (await organization.GetPermissionsByCompanyAsync(id, ct)).Values.Any(values => values.Contains(permission, StringComparer.Ordinal)),
                Access.Business => await organization.HasPermissionAsync(id, permission, ct),
                _ => false,
            };
            if (!allowed) return false;
        }
        return true;
    }
}
