using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

public sealed record PendingLegalDocumentResponse(Guid Id, LegalDocumentKind Kind, int Version);
