using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Consulta y modifica las reglas globales con evento en el mismo commit y caché posterior.</summary>
internal sealed class PlatformSettingsService(IPlatformSettingsReader reader, IPlatformSettingsRepository repository,
    IUnitOfWork unitOfWork, IRequestValidator validator, PlatformSettingsAudit audit, IPlatformSettingsCache cache,
    TimeProvider timeProvider, ILogger<PlatformSettingsService> logger) : IPlatformSettingsService
{
    public Task<Result<PlatformSettingsResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformSettingsResponse>(logger, timeProvider, "GetPlatformSettings", async () =>
        {
            var settings = await reader.FindAsync(cancellationToken);
            return settings is null ? SettingsErrors.NotFound :
                new PlatformSettingsResponse(settings.ConsumerSignup, settings.BusinessSignup, settings.MaxOwnedOrganizations, settings.Version);
        });

    public Task<Result> UpdateAsync(UpdatePlatformSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdatePlatformSettings", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var settings = await repository.GetForUpdateAsync(request.Version!.Value, ct);
                if (settings is null) return SettingsErrors.NotFound;
                var changed = settings.Update(request.ConsumerSignup!.Value, request.BusinessSignup!.Value, request.MaxOwnedOrganizations!.Value);
                if (changed.IsFailure) return changed;
                repository.MarkUpdated(settings);
                audit.Record(request.Reason!);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (result.IsSuccess) await cache.InvalidateAsync(cancellationToken);
            return result;
        });
    }
}
