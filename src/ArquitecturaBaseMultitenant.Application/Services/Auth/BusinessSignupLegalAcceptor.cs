using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Registra la aceptación vigente de términos y privacidad al crear una organización.</summary>
internal sealed class BusinessSignupLegalAcceptor(ILegalRepository legal, IRequestInfo requestInfo)
{
    internal async Task AcceptCurrentAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Terms document has not been seeded.");
        var privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Privacy document has not been seeded.");
        await AcceptIfNeededAsync(userId, terms, nowUtc, cancellationToken);
        await AcceptIfNeededAsync(userId, privacy, nowUtc, cancellationToken);
    }

    private async Task AcceptIfNeededAsync(Guid userId, LegalDocument document, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (await legal.HasAcceptedAsync(userId, document.Id, cancellationToken)) return;
        legal.AddAcceptance(LegalAcceptance.Create(userId, document, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
    }
}
