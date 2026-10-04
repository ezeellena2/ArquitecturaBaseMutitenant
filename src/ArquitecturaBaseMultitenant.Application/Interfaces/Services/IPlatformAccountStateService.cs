using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPlatformAccountStateService
{
    Task<Result> SuspendAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct);
    Task<Result> ReactivateAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct);
    Task<Result> RevokeSessionsAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct);
}
