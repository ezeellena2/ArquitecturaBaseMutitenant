using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>
/// Identifica un documento legal y su versión pendiente de aceptación. Permite que el front solicite la
/// aceptación de la versión exacta.
/// </summary>
public sealed record PendingLegalDocumentResponse(Guid Id, LegalDocumentKind Kind, int Version);
