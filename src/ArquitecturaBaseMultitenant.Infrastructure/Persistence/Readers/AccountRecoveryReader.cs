using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// Bandeja global de «Recuperar mi cuenta». Busca sin acentos y con comodines escapados por el método de antes, el
/// nuevo y el nombre de la cuenta; nunca registra el texto buscado.
/// </summary>
internal sealed class AccountRecoveryReader(ApplicationDbContext context) : IAccountRecoveryReader
{
    private static readonly SortMap<Domain.Platform.AccountRecoveryRequest> Sort = new()
    {
        ["createdAt"] = row => row.CreatedAtUtc,
    };

    public Task<PagedResult<PlatformRecoveryRow>> ListAsync(ListPlatformRecoveriesRequest request, CancellationToken ct) =>
        Rows(Filter(request).ApplySort(SortDescriptor.Parse(request.Sort), Sort,
            new SortDescriptor("createdAt", true), row => row.Id)).ToPagedResultAsync(request, ct);

    public async Task<PlatformRecoveryFilterCounts> CountAsync(ListPlatformRecoveriesRequest request, CancellationToken ct)
    {
        var statuses = await Filter(request with { Status = null }).GroupBy(row => row.Status)
            .Select(group => new PlatformRecoveryStatusCount(group.Key, group.Count())).ToArrayAsync(ct);
        return new PlatformRecoveryFilterCounts(await Filter(request).CountAsync(ct), statuses);
    }

    private IQueryable<Domain.Platform.AccountRecoveryRequest> Filter(ListPlatformRecoveriesRequest request)
    {
        var query = context.AccountRecoveryRequests.AsNoTracking();
        if (request.Status is { } status) query = query.Where(row => row.Status == status);
        if (string.IsNullOrWhiteSpace(request.Search)) return query;
        var byName = context.Users.AsNoTracking().ApplySearch(request.Search, user => user.DisplayName)
            .Select(user => user.Id);
        var byMethod = context.AccountRecoveryRequests.AsNoTracking()
            .ApplySearch(request.Search, row => row.PreviousMethodValue, row => row.NewMethodValue)
            .Select(row => row.Id);
        return query.Where(row => byMethod.Contains(row.Id) || row.UserId != null && byName.Contains(row.UserId.Value));
    }

    private IQueryable<PlatformRecoveryRow> Rows(IQueryable<Domain.Platform.AccountRecoveryRequest> query) =>
        from row in query
        select new PlatformRecoveryRow(row.Id, row.CreatedAtUtc, row.UserId,
            context.Users.Where(user => user.Id == row.UserId).Select(user => user.DisplayName).FirstOrDefault(),
            row.PreviousMethodType, row.PreviousMethodValue, row.NewMethodType, row.NewMethodValue, row.Status,
            row.DecidedAtUtc,
            context.Users.Where(user => user.Id == row.DecidedByOperatorId).Select(user => user.DisplayName).FirstOrDefault(),
            row.DecisionReason, row.Version);
}
