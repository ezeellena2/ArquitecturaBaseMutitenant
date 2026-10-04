using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Bandeja Recuperaciones de Plataforma: listado y conteos por estado.</summary>
public interface IPlatformRecoveryService
{
    Task<Result<PagedResult<PlatformRecoveryResponse>>> ListAsync(ListPlatformRecoveriesRequest request,
        CancellationToken ct);

    Task<Result<PlatformRecoveryFilterCounts>> CountAsync(ListPlatformRecoveriesRequest request, CancellationToken ct);
}

/// <summary>Aprueba o rechaza un pedido de «Recuperar mi cuenta» con motivo y versión.</summary>
public interface IPlatformRecoveryDecisionService
{
    Task<Result> ApproveAsync(Guid id, PlatformAccountActionRequest request, CancellationToken ct);

    Task<Result> RejectAsync(Guid id, PlatformAccountActionRequest request, CancellationToken ct);
}
