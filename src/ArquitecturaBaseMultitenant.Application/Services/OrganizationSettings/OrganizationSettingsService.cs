using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Lee y actualiza los datos predeterminados de la organización en una única transacción.</summary>
internal sealed class OrganizationSettingsService(
    ITenantSettingsReader reader,
    OrganizationSettingsUpdater updater,
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
        return OperationLog.RunAsync(logger, timeProvider, "UpdateOrganizationSettings",
            () => updater.UpdateAsync(request, cancellationToken));
    }

    private static OrganizationSettingsResponse ToResponse(TenantSettingsRow row) =>
        new(row.TenantName, row.DefaultCulture, row.DefaultTimeZoneId, row.DefaultCurrency, row.Version);
}
