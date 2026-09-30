using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Consulta qué documentos legales vigentes faltan aceptar a la cuenta en el instante actual.</summary>
internal sealed class LegalAcceptanceGuard(ILegalReader reader, TimeProvider timeProvider)
{
    internal Task<IReadOnlyList<PendingLegalDocumentResponse>> PendingAsync(Guid userId, CancellationToken ct) =>
        reader.ListPendingAsync(userId, timeProvider.GetUtcNow().UtcDateTime, ct);
}
