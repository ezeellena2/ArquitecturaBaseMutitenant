using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Registra únicamente consentimiento explícito, dentro de la UoW del llamador.</summary>
internal sealed class LegalConsentRecorder(ILegalRepository legal, IRequestInfo requestInfo)
{
    internal async Task<(LegalDocument Terms, LegalDocument Privacy)> GetCurrentAsync(DateTime nowUtc, CancellationToken ct)
    {
        var terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, ct)
            ?? throw new InvalidOperationException("The current Terms document has not been seeded.");
        var privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, ct)
            ?? throw new InvalidOperationException("The current Privacy document has not been seeded.");
        return (terms, privacy);
    }

    internal async Task RecordAsync(Guid userId, LegalDocument document, DateTime nowUtc, CancellationToken ct)
    {
        if (!await legal.HasAcceptedAsync(userId, document.Id, ct))
            legal.AddAcceptance(LegalAcceptance.Create(userId, document, nowUtc, requestInfo.IpAddress, requestInfo.UserAgent));
    }

    internal async Task RecordCurrentAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var documents = await GetCurrentAsync(nowUtc, ct);
        await RecordAsync(userId, documents.Terms, nowUtc, ct);
        await RecordAsync(userId, documents.Privacy, nowUtc, ct);
    }
}
