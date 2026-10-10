using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantNoticePolicy(ICurrentUser user, ILegalReader legal, AssistantAccessPolicy access, IFeatureService features,
    IPlatformPermissionService platform,
    IAssistantNoticeReader reader, TimeProvider clock)
{
    internal async Task<Result> CheckVersionAsync(string? version, CancellationToken ct)
    {
        if (user.UserId is not { } owner || user.Access is null) return UserErrors.NotFound;
        if ((await legal.ListPendingAsync(owner, clock.GetUtcNow().UtcDateTime, ct)).Count != 0)
            return LegalErrors.AcceptanceRequired;
        // Platform HTTP uses InstalledModuleRoute plus HasPlatformPermission, rather than a tenant FeatureGate.
        // Its availability policy also includes this permission; retain the contractual 403 when it is revoked.
        if (user.Access == Access.Platform && !await platform.HasPermissionAsync(owner, PlatformPermissions.Assistant.Use, ct))
            return AccessErrors.Wrong;
        if (!await features.IsEnabledAsync(AssistantFeature.Key, ct)) return TenantFeatureErrors.NotFound;
        if (!await access.CanUseAsync(user.Access, [], null, ct)) return AccessErrors.Wrong;
        return string.Equals(version, reader.CurrentVersion, StringComparison.Ordinal)
            ? Result.Success() : AssistantErrors.NoticeAcceptanceRequired;
    }

    internal Task<bool> IsAcceptedAsync(Guid ownerUserId, string version, CancellationToken ct) =>
        reader.ExistsAsync(ownerUserId, version, ct);

    internal async Task<Result> CheckAcceptedAsync(string? version, CancellationToken ct)
    {
        var authorized = await CheckVersionAsync(version, ct);
        if (authorized.IsFailure) return authorized;
        return await IsAcceptedAsync(user.UserId!.Value, reader.CurrentVersion, ct)
            ? Result.Success() : AssistantErrors.NoticeAcceptanceRequired;
    }
}
