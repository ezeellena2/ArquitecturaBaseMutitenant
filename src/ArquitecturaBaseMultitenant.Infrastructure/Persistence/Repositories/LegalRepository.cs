using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara documentos legales vigentes y aceptaciones de una cuenta dentro del caso de uso. El UnitOfWork confirma las aceptaciones junto con el alta.</summary>
internal sealed class LegalRepository(ApplicationDbContext context) : ILegalRepository
{
    public Task<bool> HasAcceptedAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.LegalAcceptances.AnyAsync(row => row.UserId == userId
            && row.LegalDocumentId == documentId, cancellationToken);
    }

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
