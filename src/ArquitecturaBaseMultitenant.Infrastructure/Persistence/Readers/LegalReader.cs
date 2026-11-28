using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// Proyecta la versión legal vigente por cultura y las aceptaciones pendientes de una cuenta. Las versiones legales y las
/// aceptaciones de la persona salen de la caché compartida (ADR 0052, 5 min) con generaciones automáticas; la vigencia se
/// evalúa en memoria contra el instante del pedido. Las consultas son de lectura y no modifican la prueba de aceptación.
/// </summary>
internal sealed class LegalReader(SharedCache cache, ApplicationDbContext context) : ILegalReader
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);
    private static readonly CacheEntry VersionsEntry =
        new(CacheKeys.Platform("legal:versions"), [CacheTags.LegalDocuments], Expiration);

    public async Task<IReadOnlyList<LegalVersionRow>> ListVersionsAsync(CancellationToken cancellationToken) =>
        await CachedVersionsAsync(cancellationToken);

    public async Task<IReadOnlyList<PlatformLegalContentResponse>> ListContentsAsync(
        Guid documentId, CancellationToken cancellationToken) =>
        await context.LegalDocumentContents.AsNoTracking()
            .Where(row => row.LegalDocumentId == documentId).OrderBy(row => row.Culture)
            .Select(row => new PlatformLegalContentResponse(row.Culture, row.Text))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<PendingLegalDocumentResponse>> ListPendingAsync(Guid userId, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var versions = await CachedVersionsAsync(cancellationToken);
        var current = CurrentVersions(versions, nowUtc);
        if (current.Count == 0) return [];
        var accepted = await CachedAcceptancesAsync(userId, cancellationToken);
        return [.. current
            .Where(document => !accepted.Any(row => row.LegalDocumentId == document.Id && row.Version == document.Version))
            .Select(document => new PendingLegalDocumentResponse(document.Id, document.Kind, document.Version))];
    }

    public async Task<LegalDocumentRow?> FindCurrentAsync(LegalDocumentKind kind,
        string culture, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        var versions = await CachedVersionsAsync(cancellationToken);
        var document = versions
            .Where(row => row.Kind == kind && row.EffectiveAtUtc <= nowUtc)
            .OrderByDescending(row => row.EffectiveAtUtc)
            .ThenByDescending(row => row.Version)
            .FirstOrDefault();
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

    private Task<LegalVersionRow[]> CachedVersionsAsync(CancellationToken cancellationToken) =>
        cache.GetOrLoadAsync(VersionsEntry, 0,
            static (services, _, ct) => LoadVersionsAsync(services.GetRequiredService<ApplicationDbContext>(), ct),
            cancellationToken);

    private Task<AcceptedLegalDocument[]> CachedAcceptancesAsync(Guid userId, CancellationToken cancellationToken) =>
        cache.GetOrLoadAsync(
            new CacheEntry(CacheKeys.User(userId, "legal:acceptances"), [CacheTags.LegalAcceptances(userId)], Expiration),
            userId,
            static (services, id, ct) => LoadAcceptancesAsync(services.GetRequiredService<ApplicationDbContext>(), id, ct),
            cancellationToken);

    private static Task<LegalVersionRow[]> LoadVersionsAsync(ApplicationDbContext db,
        CancellationToken cancellationToken) =>
        db.LegalDocuments.AsNoTracking()
            .OrderBy(row => row.Kind).ThenByDescending(row => row.Version)
            .Select(row => new LegalVersionRow(row.Id, row.Kind, row.Version, row.EffectiveAtUtc))
            .ToArrayAsync(cancellationToken);

    private static Task<AcceptedLegalDocument[]> LoadAcceptancesAsync(ApplicationDbContext db, Guid userId,
        CancellationToken cancellationToken) =>
        db.LegalAcceptances.AsNoTracking()
            .Where(row => row.UserId == userId)
            .Select(row => new AcceptedLegalDocument(row.LegalDocumentId, row.Version))
            .ToArrayAsync(cancellationToken);

    private static List<LegalVersionRow> CurrentVersions(LegalVersionRow[] versions, DateTime nowUtc)
    {
        var current = new List<LegalVersionRow>();
        foreach (var kind in Enum.GetValues<LegalDocumentKind>())
        {
            var latest = versions
                .Where(row => row.Kind == kind && row.EffectiveAtUtc <= nowUtc)
                .OrderByDescending(row => row.EffectiveAtUtc)
                .ThenByDescending(row => row.Version)
                .FirstOrDefault();
            if (latest is not null) current.Add(latest);
        }
        return current;
    }

    /// <summary>Identificadores técnicos de cada versión aceptada; sin IP, navegador ni datos personales.</summary>
    private sealed record AcceptedLegalDocument(Guid LegalDocumentId, int Version);
}
