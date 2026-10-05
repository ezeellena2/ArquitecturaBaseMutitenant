using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Consulta y modifica las reglas globales con evento en el mismo commit y caché posterior.</summary>
internal sealed class PlatformSettingsService(IPlatformSettingsReader reader, IPlatformSettingsRepository repository,
    IUnitOfWork unitOfWork, IRequestValidator validator, PlatformSettingsAuditWriter audit, IPlatformSettingsCache cache,
    TimeProvider timeProvider, ILogger<PlatformSettingsService> logger) : IPlatformSettingsService
{
    public Task<Result<PlatformSettingsResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformSettingsResponse>(logger, timeProvider, "GetPlatformSettings", async () =>
        {
            var settings = await reader.FindAsync(cancellationToken);
            return settings is null ? SettingsErrors.NotFound :
                new PlatformSettingsResponse(settings.ConsumerSignup, settings.BusinessSignup, settings.MaxOwnedOrganizations,
                    settings.SalesEmail, settings.SupportEmail, settings.Version);
        });

    public Task<Result<PlatformContactResponse>> GetContactAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformContactResponse>(logger, timeProvider, "GetPlatformContact", async () =>
        {
            var settings = await reader.FindAsync(cancellationToken);
            return settings is null ? SettingsErrors.NotFound : new PlatformContactResponse(settings.SalesEmail, settings.SupportEmail);
        });

    public Task<Result<VersionResponse>> UpdateAsync(UpdatePlatformSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, timeProvider, "UpdatePlatformSettings", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync<Result<PlatformSettings>>(async ct =>
            {
                var settings = await repository.GetForUpdateAsync(request.Version!.Value, ct);
                if (settings is null) return SettingsErrors.NotFound;
                var changes = new SecurityEventChanges();
                changes.AddEnum("consumerSignup", settings.ConsumerSignup, request.ConsumerSignup!.Value);
                changes.AddEnum("businessSignup", settings.BusinessSignup, request.BusinessSignup!.Value);
                changes.AddCount("maxOwnedOrganizations", settings.MaxOwnedOrganizations, request.MaxOwnedOrganizations);
                // Los correos son datos de contacto: el diff solo dice que cambiaron.
                if (settings.SalesEmail != request.SalesEmail) changes.AddBoolean("salesEmailChanged", false, true);
                if (settings.SupportEmail != request.SupportEmail) changes.AddBoolean("supportEmailChanged", false, true);
                var changed = settings.Update(request.ConsumerSignup!.Value, request.BusinessSignup!.Value,
                    request.MaxOwnedOrganizations!.Value, request.SalesEmail!, request.SupportEmail!);
                if (changed.IsFailure) return changed.Error;
                repository.MarkUpdated(settings);
                audit.Record(request.Reason!, changes);
                return settings;
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (result.IsSuccess) await cache.InvalidateAsync(cancellationToken);
            return VersionResponse.Of(result);
        });
    }
}
