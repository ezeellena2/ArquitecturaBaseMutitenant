using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Actualiza nombre y preferencias en una UoW y limpia la caché después del commit.</summary>
internal sealed class OrganizationSettingsUpdater(ITenantSettingsRepository settingsRepository,
    ITenantRepository tenantRepository, ITenantContext tenantContext, ITenantSettingsCache cache, IRequestValidator validator, IUnitOfWork unitOfWork)
{
    internal async Task<Result> UpdateAsync(UpdateOrganizationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;

        var tenantId = tenantContext.RequiredTenantId;
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var settings = await settingsRepository.GetCurrentAsync(request.Version!.Value, ct);
            if (settings is null) return SettingsErrors.TenantNotFound;
            var tenant = await tenantRepository.GetByIdAsync(tenantId, ct);
            if (tenant is null) return SettingsErrors.TenantNotFound;

            var renamed = tenant.Rename(request.Name);
            if (renamed.IsFailure) return renamed;
            var updated = settings.Update(request.DefaultCulture, request.DefaultTimeZoneId,
                request.DefaultCurrency);
            if (updated.IsFailure) return updated;

            // AuditTrailInterceptor deja la única entrada, con lo que cambió (B9).
            settingsRepository.MarkUpdated(settings);
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);

        if (result.IsSuccess) await cache.InvalidateAsync(tenantId, cancellationToken);
        return result;
    }
}
