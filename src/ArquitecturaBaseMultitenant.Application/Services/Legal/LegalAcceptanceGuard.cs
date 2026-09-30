using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

internal sealed class LegalAcceptanceGuard(ILegalReader reader, TimeProvider timeProvider)
{
    internal Task<IReadOnlyList<PendingLegalDocumentResponse>> PendingAsync(Guid userId, CancellationToken ct) =>
        reader.ListPendingAsync(userId, timeProvider.GetUtcNow().UtcDateTime, ct);
}
