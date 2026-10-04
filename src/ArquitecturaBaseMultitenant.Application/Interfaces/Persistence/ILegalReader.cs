using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee documentos legales vigentes y aceptaciones pendientes para mostrarlos sin exponer entidades de persistencia.</summary>
public interface ILegalReader
{
    Task<IReadOnlyList<PendingLegalDocumentResponse>> ListPendingAsync(Guid userId, DateTime nowUtc,
        CancellationToken cancellationToken);
    /// <summary>Todas las versiones de todos los tipos, sin el texto.</summary>
    Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Models.Platform.LegalVersionRow>> ListVersionsAsync(
        CancellationToken cancellationToken);

    /// <summary>Los textos de una versión, por cultura.</summary>
    Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Models.Platform.PlatformLegalContentResponse>> ListContentsAsync(
        Guid documentId, CancellationToken cancellationToken);

    Task<LegalDocumentRow?> FindCurrentAsync(LegalDocumentKind kind,
        string culture, DateTime nowUtc, CancellationToken cancellationToken);
}
