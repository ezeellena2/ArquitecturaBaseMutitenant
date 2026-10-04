using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Plataforma › Documentos legales: versiones inmutables, lectura y publicación.</summary>
public interface IPlatformLegalService
{
    Task<Result<IReadOnlyList<PlatformLegalDocumentGroupResponse>>> ListAsync(CancellationToken ct);

    Task<Result<PlatformLegalDocumentResponse>> FindAsync(Guid id, CancellationToken ct);

    Task<Result<PublishedLegalDocumentResponse>> PublishAsync(PublishLegalDocumentRequest request, CancellationToken ct);
}
