using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface ILegalService
{
    Task<Result<LegalDocumentRow>> GetCurrentAsync(LegalDocumentKind kind,
        string? culture, CancellationToken cancellationToken);
}
