using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Prepara y ejecuta el alta privada; el servicio llamador abre la única UoW de escritura.</summary>
internal sealed class PlatformTenantProvisioning(PlatformActionGuard guard, PlatformTenantCreator creator,
    PlatformTenantActivator activator, IPersonalSpaceProvisioner defaults)
{
    internal async Task<TenantSettings> PrepareAsync(string? culture, string? zone, CancellationToken ct) =>
        (await defaults.PrepareAsync(culture, zone, ct)).Settings;
    internal Task<Result<PlatformActionGuard.Lease>> EnterAsync(Guid id, string reason, CancellationToken ct) => guard.EnterAsync(id, reason, ct);
    internal Task<Result<CreatedPlatformTenantResponse>> CreateAsync(Guid id, Guid operatorId, CreatePlatformTenantRequest request,
        TenantSettings settings, string reason, CancellationToken ct) => creator.CreateAsync(id, operatorId, request, settings, reason, ct);
    internal Task<Result> ActivateAsync(Guid id, bool retry, PlatformTenantStateRequest request, TenantSettings settings,
        DateTime nowUtc, CancellationToken ct) => activator.ActivateAsync(id, retry, request, settings, nowUtc, ct);
}
