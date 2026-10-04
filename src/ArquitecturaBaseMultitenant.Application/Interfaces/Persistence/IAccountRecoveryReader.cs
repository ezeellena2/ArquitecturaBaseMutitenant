using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee la bandeja global de «Recuperar mi cuenta» con el nombre actual de la cuenta y de quien resolvió.</summary>
public interface IAccountRecoveryReader
{
    Task<PagedResult<PlatformRecoveryRow>> ListAsync(ListPlatformRecoveriesRequest request, CancellationToken ct);

    Task<PlatformRecoveryFilterCounts> CountAsync(ListPlatformRecoveriesRequest request, CancellationToken ct);
}
