using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Personal usa la habilitación global; Business delega al grant vigente; Plataforma exige su permiso individual.</summary>
internal sealed class AssistantAvailabilityPolicy(IAssistantSettingsReader settings, ICurrentUser user, IPlatformPermissionService permissions) : IFeatureAvailabilityPolicy
{
    public string Key => "assistant";
    public async Task<bool?> IsAvailableAsync(Access access, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return access switch
        {
            Access.Consumer => await settings.IsPersonalEnabledAsync(cancellationToken),
            Access.Business => null,
            Access.Platform => user.UserId is { } userId && await permissions.HasPermissionAsync(userId, PlatformPermissions.Assistant.Use, cancellationToken),
            _ => false,
        };
    }
}
