using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantCapabilityContributor(ICurrentUser user, IPlatformPermissionService permissions, IFeatureService features) : IModuleCapabilityContributor
{
    public async Task<IReadOnlyList<ModuleCapabilityResponse>> ListAsync(CancellationToken cancellationToken)
    {
        if (user.Access != Access.Platform || user.UserId is not { } userId) return [];
        var configure = await permissions.HasPermissionAsync(userId, PlatformPermissions.Assistant.Configure, cancellationToken);
        var read = await permissions.HasPermissionAsync(userId, PlatformPermissions.Assistant.Read, cancellationToken);
        var available = await features.IsEnabledAsync("assistant", cancellationToken);
        return [new ModuleCapabilityResponse("assistant", available, configure, read)];
    }
}
