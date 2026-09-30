using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Bloquea la cuenta, relee las versiones legales vigentes y guarda solo aceptaciones de esas versiones para evitar consentimientos obsoletos.</summary>
internal sealed class LegalAcceptanceWriter(ILegalRepository legal, ILoginMethodRepository methods,
    IRequestInfo requestInfo, TimeProvider timeProvider)
{
    internal async Task<Result> AcceptAsync(Guid userId, AcceptLegalRequest request, CancellationToken innerCt)
    {
        await methods.LockUserAsync(userId, innerCt);
        var current = new List<LegalDocument>();
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var kind in Enum.GetValues<LegalDocumentKind>())
        {
            var document = await legal.GetCurrentDocumentAsync(kind, nowUtc, innerCt);
            if (document is null) return LegalErrors.DocumentNotFound;
            current.Add(document);
        }
        if (request.Documents!.Any(item => !current.Any(document => document.Id == item.Id
            && document.Version == item.Version))) return LegalErrors.VersionChanged;
        foreach (var document in current)
        {
            if (await legal.HasAcceptedAsync(userId, document.Id, innerCt)) continue;
            if (!request.Documents!.Any(item => item.Id == document.Id && item.Version == document.Version))
                return LegalErrors.VersionChanged;
            legal.AddAcceptance(LegalAcceptance.Create(userId, document, nowUtc,
                requestInfo.IpAddress, requestInfo.UserAgent));
        }
        return Result.Success();
    }
}

