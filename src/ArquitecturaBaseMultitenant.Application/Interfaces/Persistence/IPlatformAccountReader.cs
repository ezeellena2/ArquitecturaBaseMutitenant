using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Proyección global de cuentas para Plataforma: identidad, métodos, índice de accesos e ingresos, sin leer
/// tablas privadas de organizaciones. <c>signInTypes</c> son los tipos de método con canal
/// configurado; con ellos se deriva NeedsRecovery en la misma consulta que filtra y cuenta.
/// </summary>
public interface IPlatformAccountReader
{
    Task<PagedResult<PlatformAccountRow>> ListAsync(ListPlatformAccountsRequest request,
        IReadOnlyCollection<LoginMethodType> signInTypes, CancellationToken ct);

    Task<PlatformAccountFilterCounts> CountAsync(ListPlatformAccountsRequest request,
        IReadOnlyCollection<LoginMethodType> signInTypes, CancellationToken ct);

    Task<PlatformAccountDetailRow?> FindAsync(Guid accountId, IReadOnlyCollection<LoginMethodType> signInTypes,
        CancellationToken ct);
}
