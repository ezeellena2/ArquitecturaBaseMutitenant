using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>Publica sólo la versión base de demostración; jamás modifica un texto publicado.</summary>
internal sealed class LegalDocumentSeeder(
    ApplicationDbContext context,
    TimeProvider timeProvider)
{
    private const int BaseVersion = 1;

    public async Task SeedAsync(IReadOnlyList<CultureCatalogEntry> cultures, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cultures);
        context.RequireTransaction();
        var enabledCultures = cultures
            .Where(culture => culture.IsEnabled).ToArray();
        if (enabledCultures.Length == 0)
        {
            throw new InvalidOperationException("The reference catalog has no enabled culture for legal documents.");
        }

        foreach (var kind in new[] { LegalDocumentKind.Terms, LegalDocumentKind.Privacy })
        {
            if (await context.LegalDocuments.AnyAsync(document =>
                    document.Kind == kind && document.Version == BaseVersion, cancellationToken))
            {
                continue;
            }

            var document = LegalDocument.Create(kind, BaseVersion, timeProvider.GetUtcNow().UtcDateTime);
            context.LegalDocuments.Add(document);
            foreach (var culture in enabledCultures)
            {
                context.LegalDocumentContents.Add(LegalDocumentContent.Create(document.Id, culture.Code,
                    Text(kind, culture.LanguageCode)));
            }
        }
    }

    private static string Text(LegalDocumentKind kind, string language) => (kind, language) switch
    {
        (LegalDocumentKind.Terms, "es") =>
            "DOCUMENTO DE DEMOSTRACIÓN. Esta plantilla no incluye términos legales. Reemplazá este texto antes de publicar.",
        (LegalDocumentKind.Privacy, "es") =>
            "DOCUMENTO DE DEMOSTRACIÓN. Esta plantilla no incluye una política de privacidad. Reemplazá este texto antes de publicar.",
        (LegalDocumentKind.Terms, "en") =>
            "DEMONSTRATION DOCUMENT. This template does not include legal terms. Replace this text before publishing.",
        (LegalDocumentKind.Privacy, "en") =>
            "DEMONSTRATION DOCUMENT. This template does not include a privacy policy. Replace this text before publishing.",
        _ => throw new InvalidOperationException($"No demonstration legal text for language '{language}'."),
    };
}
