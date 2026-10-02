using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

internal sealed class TenantSlugAvailabilityService(ITenantSlugAvailabilityReader slugs,
    TimeProvider timeProvider, ILogger<TenantSlugAvailabilityService> logger) : ITenantSlugAvailabilityService
{
    public Task<Result<TenantSlugAvailabilityResponse>> CheckAsync(string? value,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<TenantSlugAvailabilityResponse>(logger, timeProvider,
            "CheckTenantSlugAvailability", async () =>
            {
                var parsed = TenantSlug.Create(value);
                if (parsed.IsFailure) return parsed.Error;

                var slug = parsed.Value.Value;
                var availability = await slugs.IsReservedAsync(slug, cancellationToken)
                    ? TenantSlugAvailability.Reserved
                    : await slugs.IsTakenAsync(slug, cancellationToken)
                        ? TenantSlugAvailability.Taken
                        : TenantSlugAvailability.Available;
                return new TenantSlugAvailabilityResponse(slug, availability);
            });
}
