using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyecta la versión legal vigente por cultura y las aceptaciones pendientes de una cuenta. Las consultas son de lectura y no modifican la prueba de aceptación.</summary>
internal sealed class LegalReader(ApplicationDbContext context) : ILegalReader
{
    public async Task<IReadOnlyList<PendingLegalDocumentResponse>> ListPendingAsync(Guid userId, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var result = new List<PendingLegalDocumentResponse>();
        foreach (var kind in Enum.GetValues<LegalDocumentKind>())
        {
            var current = await context.LegalDocuments.AsNoTracking()
                .Where(row => row.Kind == kind && row.EffectiveAtUtc <= nowUtc)
                .OrderByDescending(row => row.EffectiveAtUtc).ThenByDescending(row => row.Version)
                .Select(row => new PendingLegalDocumentResponse(row.Id, row.Kind, row.Version))
                .FirstOrDefaultAsync(cancellationToken);
            if (current is not null && !await context.LegalAcceptances.AsNoTracking()
                .AnyAsync(row => row.UserId == userId && row.LegalDocumentId == current.Id
                    && row.Version == current.Version, cancellationToken)) result.Add(current);
        }
        return result;
    }

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
