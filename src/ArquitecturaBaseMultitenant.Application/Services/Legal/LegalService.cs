using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Lee la versión pública vigente usando sólo culturas habilitadas del catálogo.</summary>
internal sealed class LegalService(
    ILegalReader reader,
    ICultureCatalog cultures,
    TimeProvider timeProvider,
    ILogger<LegalService> logger) : ILegalService
{
    public Task<Result<LegalDocumentRow>> GetCurrentAsync(LegalDocumentKind kind,
        string? culture, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<LegalDocumentRow>(logger, timeProvider, "GetCurrentLegalDocument", async () =>
        {
            if (!Enum.IsDefined(kind)) return LegalErrors.DocumentNotFound;
            var selected = await new CultureProfiles(cultures).LoadAsync(culture, cancellationToken);
            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var cultureCode in selected.TranslationOrder)
            {
                var document = await reader.FindCurrentAsync(kind, cultureCode, nowUtc, cancellationToken);
                if (document is not null) return document;
            }

            return LegalErrors.DocumentNotFound;
        });
}
