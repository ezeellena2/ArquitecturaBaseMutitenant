using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ILegalReader
{
    Task<LegalDocumentRow?> FindCurrentAsync(LegalDocumentKind kind,
        string culture, DateTime nowUtc, CancellationToken cancellationToken);
}
