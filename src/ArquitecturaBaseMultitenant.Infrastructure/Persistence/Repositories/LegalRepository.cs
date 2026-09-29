using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class LegalRepository(ApplicationDbContext context) : ILegalRepository
{
    public Task<LegalDocument?> GetCurrentDocumentAsync(LegalDocumentKind kind,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.LegalDocuments
            .Where(document => document.Kind == kind && document.EffectiveAtUtc <= nowUtc)
            .OrderByDescending(document => document.EffectiveAtUtc)
            .ThenByDescending(document => document.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void AddDocument(LegalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        context.RequireTransaction();
        context.LegalDocuments.Add(document);
    }

    public void AddContent(LegalDocumentContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        context.RequireTransaction();
        context.LegalDocumentContents.Add(content);
    }

    public void AddAcceptance(LegalAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        context.RequireTransaction();
        context.LegalAcceptances.Add(acceptance);
    }
}
