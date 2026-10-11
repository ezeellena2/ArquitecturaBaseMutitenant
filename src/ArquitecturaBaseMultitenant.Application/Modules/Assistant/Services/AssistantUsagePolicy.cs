using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Management authorization is independent of chat use, notice, configuration and provider availability.</summary>
internal sealed class AssistantUsagePolicy(ICurrentUser user, AssistantSessionPolicy session,
    IFeatureService features, IPermissionService organization, IPlatformPermissionService platform)
{
    internal async Task<Result> CheckAsync(Access expected, CancellationToken cancellationToken)
    {
        if (user.UserId is not { } owner || user.Access != expected || !await session.IsActiveAsync(cancellationToken))
            return AccessErrors.Wrong;
        if (expected == Access.Business)
        {
            if (!await organization.HasPermissionAsync(owner, Permissions.Assistant.Configure, cancellationToken)) return AccessErrors.Wrong;
            if (!await features.IsEnabledAsync(AssistantFeature.Key, cancellationToken)) return TenantFeatureErrors.NotFound;
        }
        else if (!await platform.HasPermissionAsync(owner, PlatformPermissions.Assistant.Read, cancellationToken)) return AccessErrors.Wrong;
        return Result.Success();
    }
}
