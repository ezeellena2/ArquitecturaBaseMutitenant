using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPlatformAccountService
{
    Task<Result<PagedResult<PlatformAccountResponse>>> ListAsync(ListPlatformAccountsRequest request, CancellationToken ct);
    Task<Result<PlatformAccountFilterCounts>> CountAsync(ListPlatformAccountsRequest request, CancellationToken ct);
    Task<Result<PlatformAccountDetailResponse>> FindAsync(Guid accountId, CancellationToken ct);
}
