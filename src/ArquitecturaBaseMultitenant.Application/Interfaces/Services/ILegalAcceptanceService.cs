using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Consulta documentos pendientes y registra su aceptación para que una sesión continúe con las versiones vigentes.</summary>
public interface ILegalAcceptanceService
{
    Task<Result<IReadOnlyList<PendingLegalDocumentResponse>>> GetPendingAsync(CancellationToken ct);
    Task<Result> AcceptAsync(AcceptLegalRequest request, CancellationToken ct);
}
