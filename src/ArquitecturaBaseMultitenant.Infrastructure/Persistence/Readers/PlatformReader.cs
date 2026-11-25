using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Lee exclusivamente metadatos globales de organizaciones y el índice de accesos.</summary>
internal sealed class PlatformReader(ApplicationDbContext context) : IPlatformReader
{
    private static readonly SortMap<TenantSortRow> Sort = new()
    {
        ["name"] = t => t.Name,
        ["createdAtUtc"] = t => t.CreatedAtUtc,
        ["ownerName"] = t => t.Owner,
        ["companyCount"] = t => t.CompanyCount,
        ["memberCount"] = t => t.MemberCount,
        ["status"] = t => t.Status,
    };

    public async Task<PagedResult<PlatformTenantResponse>> ListTenantsAsync(ListPlatformTenantsRequest request, CancellationToken ct)
    {
        var page = await (from t in ApplyFilters(request)
                          join counters in context.TenantCounters.AsNoTracking() on t.Id equals counters.TenantId
                          let ownerId = context.UserTenantAccesses.Where(a => a.TenantId == t.Id && a.Status == MemberStatus.Active
                              && a.RoleName == SystemRoles.TenantAdmin).OrderBy(a => a.JoinedAtUtc).ThenBy(a => a.UserId)
                              .Select(a => (Guid?)a.UserId).FirstOrDefault()
                          let userId = ownerId ?? (t.RegistrationSource == TenantRegistrationSource.SelfService ? t.RegisteredByUserId : null)
                          let ownerName = context.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefault()
                          // La columna «Dueño» muestra el nombre o, sin nombre, el correo (el del Dueño invitado, sin cuenta).
                          let ownerEmail = context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefault()
                              ?? context.PendingTenantOwners.Where(owner => owner.TenantId == t.Id)
                                  .OrderBy(owner => owner.InvitationId).Select(owner => owner.Address).FirstOrDefault()
                          select new TenantSortRow
                          {
                              Id = t.Id,
                              Name = t.Name,
                              CreatedAtUtc = t.CreatedAtUtc,
                              Owner = ownerName != null && ownerName != "" ? ownerName : ownerEmail,
                              CompanyCount = counters.CompanyCount,
                              MemberCount = counters.MemberCount,
                              Status = t.Status,
                          })
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, new SortDescriptor("name", false), t => t.Id)
            .Select(t => t.Id)
            .ToPagedResultAsync(request, ct);
        if (page.Items.Count == 0) return new PagedResult<PlatformTenantResponse>([], page.Page, page.PageSize, page.TotalCount);

        Guid[] ids = [.. page.Items];
        var rows = await Project(context.Tenants.AsNoTracking().Where(t => ids.Contains(t.Id))).ToDictionaryAsync(t => t.Id, ct);
        return new PagedResult<PlatformTenantResponse>([.. page.Items.Select(id => rows[id])], page.Page, page.PageSize,
            page.TotalCount);
    }

    public async Task<PlatformTenantFilterCounts> CountTenantsAsync(ListPlatformTenantsRequest request, CancellationToken ct)
    {
        var statuses = await ApplyFilters(request with { Status = null }).GroupBy(t => t.Status)
            .Select(g => new PlatformTenantStatusCount(g.Key, g.Count())).ToArrayAsync(ct);
        var sources = await ApplyFilters(request with { Source = null }).GroupBy(t => t.RegistrationSource)
            .Select(g => new PlatformTenantSourceCount(g.Key, g.Count())).ToArrayAsync(ct);
        return new PlatformTenantFilterCounts(await ApplyFilters(request).CountAsync(ct), statuses, sources);
    }

    public Task<PlatformTenantResponse?> FindTenantAsync(Guid id, CancellationToken ct) =>
        Project(context.Tenants.AsNoTracking().Where(t => t.Kind == TenantKind.Business && t.Id == id)).SingleOrDefaultAsync(ct);

    public async Task<PlatformTenantDetailRow?> FindTenantDetailAsync(Guid id, CancellationToken ct)
    {
        var tenant = await FindTenantAsync(id, ct);
        if (tenant is null) return null;
        var owners = await (from access in context.UserTenantAccesses.AsNoTracking()
                            join user in context.Users.AsNoTracking() on access.UserId equals user.Id
                            where access.TenantId == id && access.Status == MemberStatus.Active && access.RoleName == SystemRoles.TenantAdmin
                            orderby access.JoinedAtUtc, access.UserId
                            select new PlatformTenantOwnerResponse(user.Id, user.DisplayName,
                                user.Email == null ? null : Email.Create(user.Email).Value, user.Status, access.Status,
                                context.LoginAudits.Where(audit => audit.UserId == user.Id && audit.Succeeded)
                                    .OrderByDescending(audit => audit.OccurredAtUtc).ThenByDescending(audit => audit.Id)
                                    .Select(audit => (DateTime?)audit.OccurredAtUtc).FirstOrDefault(),
                                context.LoginAudits.Where(audit => audit.UserId == user.Id && audit.Succeeded)
                                    .OrderByDescending(audit => audit.OccurredAtUtc).ThenByDescending(audit => audit.Id)
                                    .Select(audit => (LoginAuditMethod?)audit.Method).FirstOrDefault())).ToArrayAsync(ct);
        // El jsonb conserva evidencia técnica. Una entrada, un cambio de módulos o un replay sin transición
        // no reemplazan el último cambio de estado; las filas históricas sin evidencia quedan fuera.
        var state = await context.SecurityEvents.FromSql($"""
            SELECT * FROM platform."SecurityEvents"
            WHERE "TargetTenantId" = {id}
              AND "Changes" -> 'status' ->> 'after' IS NOT NULL
              AND ("Changes" -> 'status' ->> 'before') IS DISTINCT FROM ("Changes" -> 'status' ->> 'after')
            """).AsNoTracking()
            .Where(item => item.Type == SecurityEventType.OrganizationCreated
                || item.Type == SecurityEventType.OrganizationApproved || item.Type == SecurityEventType.OrganizationRejected
                || item.Type == SecurityEventType.OrganizationSuspended || item.Type == SecurityEventType.OrganizationReactivated
                || item.Type == SecurityEventType.OrganizationClosed || item.Type == SecurityEventType.OrganizationProvisioningRetried)
            .OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Select(item => new PlatformTenantStateChangeRow(item.OccurredAtUtc, item.Type, item.Reason)).FirstOrDefaultAsync(ct);
        var pendingEmail = owners.Length == 0 ? await context.PendingTenantOwners.AsNoTracking()
            .Where(owner => owner.TenantId == id).OrderBy(owner => owner.InvitationId)
            .Select(owner => owner.Address).FirstOrDefaultAsync(ct) : null;
        return new PlatformTenantDetailRow(tenant, owners, state,
            pendingEmail is null ? null : Email.Create(pendingEmail).Value);
    }

    public async Task<IReadOnlyList<Guid>> ListActiveTenantOwnerIdsAsync(Guid id, CancellationToken ct) =>
        await context.UserTenantAccesses.AsNoTracking().Where(a => a.TenantId == id
            && a.Status == MemberStatus.Active && a.RoleName == SystemRoles.TenantAdmin
            && context.Users.Any(user => user.Id == a.UserId && user.Status == UserStatus.Active))
            .Select(a => a.UserId).Distinct().Order().ToArrayAsync(ct);

    private IQueryable<Tenant> ApplyFilters(ListPlatformTenantsRequest request)
    {
        var query = context.Tenants.AsNoTracking().Where(t => t.Kind == TenantKind.Business);
        if (request.Status is { } status) query = query.Where(t => t.Status == status);
        if (request.Source is { } source) query = query.Where(t => t.RegistrationSource == source);
        if (request.OnlyWithCreationDate) query = query.Where(t => t.CreatedAtUtc != null);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.ApplySearch(request.Search, t => t.Name, t => t.Slug,
                t => context.Users.Where(u => u.Id == (context.UserTenantAccesses
                    .Where(a => a.TenantId == t.Id && a.Status == MemberStatus.Active && a.RoleName == SystemRoles.TenantAdmin)
                    .OrderBy(a => a.JoinedAtUtc).ThenBy(a => a.UserId).Select(a => (Guid?)a.UserId).FirstOrDefault()
                    ?? (t.RegistrationSource == TenantRegistrationSource.SelfService ? t.RegisteredByUserId : null)))
                    .Select(u => u.DisplayName).FirstOrDefault(),
                t => context.Users.Where(u => u.Id == (context.UserTenantAccesses
                    .Where(a => a.TenantId == t.Id && a.Status == MemberStatus.Active && a.RoleName == SystemRoles.TenantAdmin)
                    .OrderBy(a => a.JoinedAtUtc).ThenBy(a => a.UserId).Select(a => (Guid?)a.UserId).FirstOrDefault()
                    ?? (t.RegistrationSource == TenantRegistrationSource.SelfService ? t.RegisteredByUserId : null)))
                    .Select(u => u.Email).FirstOrDefault(),
                t => context.PendingTenantOwners.Where(owner => owner.TenantId == t.Id)
                    .OrderBy(owner => owner.InvitationId).Select(owner => owner.Address).FirstOrDefault());
        }
        return query;
    }

    private IQueryable<PlatformTenantResponse> Project(IQueryable<Tenant> query) =>
        from t in query
        join counters in context.TenantCounters.AsNoTracking() on t.Id equals counters.TenantId
        let ownerId = context.UserTenantAccesses.Where(a => a.TenantId == t.Id && a.Status == MemberStatus.Active
            && a.RoleName == SystemRoles.TenantAdmin).OrderBy(a => a.JoinedAtUtc).ThenBy(a => a.UserId)
            .Select(a => (Guid?)a.UserId).FirstOrDefault()
        let userId = ownerId ?? (t.RegistrationSource == TenantRegistrationSource.SelfService ? t.RegisteredByUserId : null)
        // Sin Dueño activo ni registrante, el contacto es el Dueño invitado desde Plataforma (sin nombre).
        let ownerEmail = context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefault()
            ?? context.PendingTenantOwners.Where(owner => owner.TenantId == t.Id)
                .OrderBy(owner => owner.InvitationId).Select(owner => owner.Address).FirstOrDefault()
        select new PlatformTenantResponse(t.Id, t.Name, t.Slug, t.Status, t.RegistrationSource,
            t.CreatedAtUtc, counters.CompanyCount, counters.MemberCount, t.Version,
            context.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefault(),
            ownerEmail == null ? null : Email.Create(ownerEmail).Value);

    /// <summary>Una organización con lo que se ordena: sus datos, el Dueño que muestra la tabla y sus contadores.</summary>
    private sealed class TenantSortRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateTime? CreatedAtUtc { get; init; }
        public string? Owner { get; init; }
        public int CompanyCount { get; init; }
        public int MemberCount { get; init; }
        public TenantStatus Status { get; init; }
    }
}
