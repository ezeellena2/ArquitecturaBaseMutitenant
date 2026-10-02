using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Lee y actualiza los datos predeterminados de la organización en una única transacción.</summary>
internal sealed class OrganizationSettingsService(
    ITenantSettingsReader reader,
    ITenantSettingsRepository settingsRepository,
    ITenantRepository tenantRepository,
    ITenantContext tenantContext,
    IAuditLog auditLog,
    ITenantSettingsCache cache,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<OrganizationSettingsService> logger) : IOrganizationSettingsService
{
    public Task<Result<OrganizationSettingsResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<OrganizationSettingsResponse>(logger, timeProvider,
            "GetOrganizationSettings", async () =>
            {
                var settings = await reader.FindCurrentAsync(cancellationToken);
                return settings is null ? SettingsErrors.TenantNotFound : ToResponse(settings);
            });

    public Task<Result> UpdateAsync(UpdateOrganizationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdateOrganizationSettings", async () =>
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

                settingsRepository.MarkUpdated(settings);
                auditLog.Record(AuditAction.Updated, "TenantSettings", settings.Id,
                    new Dictionary<string, object?>
                    {
                        ["name"] = tenant.Name,
                        ["defaultCulture"] = settings.DefaultCulture,
                        ["defaultTimeZoneId"] = settings.DefaultTimeZoneId,
                        ["defaultCurrency"] = settings.DefaultCurrency,
                    });
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);

            if (result.IsSuccess) await cache.InvalidateAsync(tenantId, cancellationToken);
            return result;
        });
    }

    private static OrganizationSettingsResponse ToResponse(TenantSettingsRow row) =>
        new(row.TenantName, row.DefaultCulture, row.DefaultTimeZoneId, row.DefaultCurrency, row.Version);
}
