using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Consulta y agrega versiones legales y aceptaciones dentro de la transacción del caso de uso.</summary>
public interface ILegalRepository
{
    Task<bool> HasAcceptedAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
    Task<LegalDocument?> GetCurrentDocumentAsync(LegalDocumentKind kind,
        DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>Lock global por tipo: serializa la numeración de las versiones nuevas.</summary>
    Task LockKindAsync(LegalDocumentKind kind, CancellationToken cancellationToken);

    /// <summary>El número de la última versión del tipo, vigente o futura; 0 si no hay ninguna.</summary>
    Task<int> GetLatestVersionAsync(LegalDocumentKind kind, CancellationToken cancellationToken);

    void AddDocument(LegalDocument document);

    void AddContent(LegalDocumentContent content);

    void AddAcceptance(LegalAcceptance acceptance);
}
