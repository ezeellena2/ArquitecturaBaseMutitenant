using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ILegalRepository
{
    Task<LegalDocument?> GetCurrentDocumentAsync(LegalDocumentKind kind,
        DateTime nowUtc, CancellationToken cancellationToken);

    void AddDocument(LegalDocument document);

    void AddContent(LegalDocumentContent content);

    void AddAcceptance(LegalAcceptance acceptance);
}
