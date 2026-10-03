using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class OrganizationAuditReader(ApplicationDbContext context, ITenantContext tenantContext)
    : IOrganizationAuditReader
{
    private static readonly Dictionary<string, string[]> Categories = new(StringComparer.Ordinal)
    {
        ["users"] = ["Member", "Invitation"],
        ["roles"] = ["Role", "RoleAssignment"],
        ["companies"] = ["Company", "CompanyMembership"],
        ["settings"] = ["TenantSettings", "TenantDomain"],
        ["organization"] = ["PublicPage", "Tenant"],
    };

    public async Task<CursorResult<OrganizationAuditRow>> ListAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var entries = await FilteredAsync(request, startUtc, endUtc, cancellationToken);
        if (request.ActorId is { } actorId)
        {
            entries = entries.Where(entry => entry.ActorId == actorId);
        }

        var page = await entries.ToCursorResultAsync(request, entry => entry.OccurredAtUtc,
            entry => entry.Id, cancellationToken);
        var actorIds = page.Items.Where(entry => entry.ActorKind == AuditActorKind.User && entry.ActorId != null)
            .Select(entry => entry.ActorId!.Value).Distinct().ToArray();
        var actors = actorIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await context.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        var rows = page.Items.Select(entry => new OrganizationAuditRow(entry.Id, entry.OccurredAtUtc,
            entry.Action, entry.EntityType, entry.EntityId, entry.ActorId, entry.ActorKind,
            entry.ActorId is { } id ? actors.GetValueOrDefault(id) : null, entry.Changes)).ToArray();
        return new CursorResult<OrganizationAuditRow>(rows, page.NextCursor, page.HasMore);
    }

    public async Task<IReadOnlyList<OrganizationAuditActorRow>> ListActorsAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var actors = await (await FilteredAsync(request with { ActorId = null }, startUtc, endUtc,
                cancellationToken))
            .Select(entry => new { entry.ActorId, entry.ActorKind })
            .Distinct().ToArrayAsync(cancellationToken);
        var actorIds = actors.Where(actor => actor.ActorKind == AuditActorKind.User && actor.ActorId != null)
            .Select(actor => actor.ActorId!.Value).Distinct().ToArray();
        var names = actorIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await context.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        return actors.Select(actor => new OrganizationAuditActorRow(actor.ActorId, actor.ActorKind,
                actor.ActorId is { } id ? names.GetValueOrDefault(id) : null))
            .OrderBy(actor => actor.ActorKind)
            .ThenBy(actor => actor.DisplayName, StringComparer.CurrentCulture)
            .ToArray();
    }

    private async Task<IQueryable<AuditEntry>> FilteredAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        IQueryable<AuditEntry> entries;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            var matchingActorIds = await context.Users.AsNoTracking()
                .Where(user => user.DisplayName != null && EF.Functions.ILike(user.DisplayName, pattern, "\\"))
                .Select(user => user.Id).ToArrayAsync(cancellationToken);
            entries = context.Set<AuditEntry>().FromSqlInterpolated($"""
                SELECT * FROM tenant."AuditEntries"
                WHERE "TenantId" = {tenantId}
                  AND ("EntityType" ILIKE {pattern} ESCAPE E'\\'
                    OR "Changes"::text ILIKE {pattern} ESCAPE E'\\'
                    OR "ActorId" = ANY ({matchingActorIds}))
                """).AsNoTracking();
        }
        else
        {
            entries = context.Set<AuditEntry>().AsNoTracking();
        }

        entries = entries.Where(entry => entry.TenantId == tenantId);
        if (startUtc is { } start) entries = entries.Where(entry => entry.OccurredAtUtc >= start);
        if (endUtc is { } end) entries = entries.Where(entry => entry.OccurredAtUtc < end);
        if (request.Category is { } category)
        {
            var entityTypes = Categories[category];
            entries = entries.Where(entry => entityTypes.Contains(entry.EntityType));
        }

        return entries;
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
