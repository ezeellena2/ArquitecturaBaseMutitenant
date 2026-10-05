using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPlatformAccountStateService
{
    Task<Result> SuspendAsync(Guid accountId, PlatformAccountReasonRequest request, CancellationToken ct);
    Task<Result> ReactivateAsync(Guid accountId, PlatformAccountReasonRequest request, CancellationToken ct);
    Task<Result> RevokeSessionsAsync(Guid accountId, PlatformAccountReasonRequest request, CancellationToken ct);
}
