using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface ILegalAcceptanceService
{
    Task<Result<IReadOnlyList<PendingLegalDocumentResponse>>> GetPendingAsync(CancellationToken ct);
    Task<Result> AcceptAsync(AcceptLegalRequest request, CancellationToken ct);
}
