using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPlatformAccountDeletionService
{
    Task<Result> RequestDeletionAsync(Guid accountId, PlatformAccountReasonRequest request, CancellationToken ct);
}
