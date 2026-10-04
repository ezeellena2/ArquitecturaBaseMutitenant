using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformFeatureService(PlatformFeatureListing listing, PlatformFeatureWriter writer,
    IUnitOfWork unitOfWork, IRequestValidator validator, ITenantFeatureCache cache,
    TimeProvider clock, ILogger<PlatformFeatureService> logger) : IPlatformFeatureService
{
    public Task<Result<IReadOnlyList<PlatformFeatureResponse>>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "ListPlatformFeatures", () => listing.ListAsync(tenantId, cancellationToken));

    public Task<Result> UpdateAsync(Guid tenantId, string key, UpdatePlatformFeatureRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "UpdatePlatformFeature", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            try
            {
                var result = await unitOfWork.ExecuteInTransactionAsync(ct => writer.UpdateAsync(tenantId, key, request, ct),
                    CommitPolicy.OnSuccess, cancellationToken);
                if (result.IsSuccess) await cache.InvalidateAsync(tenantId, cancellationToken);
                return result;
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.TenantFeature)
            {
                throw new ConcurrencyConflictException("The initial module configuration was changed concurrently.", exception);
            }
        });
}
