using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface ITenantSlugAvailabilityService
{
    Task<Result<TenantSlugAvailabilityResponse>> CheckAsync(string? value, CancellationToken cancellationToken);
}
