using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class LegalReader(ApplicationDbContext context) : ILegalReader
{
    public async Task<LegalDocumentRow?> FindCurrentAsync(LegalDocumentKind kind,
        string culture, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        var document = await context.LegalDocuments.AsNoTracking()
            .Where(row => row.Kind == kind && row.EffectiveAtUtc <= nowUtc)
            .OrderByDescending(row => row.EffectiveAtUtc)
            .ThenByDescending(row => row.Version)
            .Select(row => new { row.Id, row.Kind, row.Version, row.EffectiveAtUtc })
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return null;
        }

        var text = await context.LegalDocumentContents.AsNoTracking()
            .Where(row => row.LegalDocumentId == document.Id && row.Culture == culture)
            .Select(row => row.Text)
            .SingleOrDefaultAsync(cancellationToken);
        return text is null ? null : new LegalDocumentRow(document.Id, document.Kind,
            document.Version, document.EffectiveAtUtc, culture, text);
    }
}
