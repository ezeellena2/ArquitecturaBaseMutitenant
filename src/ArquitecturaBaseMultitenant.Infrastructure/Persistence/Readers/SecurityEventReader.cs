using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// Auditoría de seguridad global. Une en SQL las claves de SecurityEvents y LoginAudits (fecha e Id) para paginar con
/// un solo cursor estable y después completa la página con los nombres actuales. Nunca lee códigos ni direcciones:
/// LoginAudits no los guarda y los SecurityEvent tienen la IP enmascarada.
/// </summary>
internal sealed class SecurityEventReader(ApplicationDbContext context) : ISecurityEventReader
{
    public async Task<CursorResult<PlatformAuditRow>> ListAsync(ListPlatformAuditRequest request, DateTime? sinceUtc,
        IReadOnlyCollection<SecurityEventType>? eventTypes, bool includeSignIns, CancellationToken ct)
    {
        var page = await Keys(request, sinceUtc, eventTypes, includeSignIns)
            .ToCursorResultAsync(request, key => key.OccurredAtUtc, key => key.Id, ct);
        var rows = await LoadAsync(page.Items.Where(key => !key.IsSignIn).Select(key => key.Id).ToArray(),
            page.Items.Where(key => key.IsSignIn).Select(key => key.Id).ToArray(), ct);
        return new CursorResult<PlatformAuditRow>([.. page.Items.Select(key => rows[key.Id])], page.NextCursor, page.HasMore);
    }

    public async Task<PlatformAuditRow?> FindAsync(Guid id, CancellationToken ct)
    {
        var rows = await LoadAsync([id], [id], ct);
        return rows.GetValueOrDefault(id);
    }

    public async Task<IReadOnlyList<PlatformAuditOrganizationResponse>> ListOrganizationsAsync(
        ListPlatformAuditRequest request, DateTime? sinceUtc, IReadOnlyCollection<SecurityEventType>? eventTypes,
        CancellationToken ct)
    {
        var ids = FilteredEvents(request with { TenantId = null }, sinceUtc, eventTypes)
            .Where(item => item.TargetTenantId != null).Select(item => item.TargetTenantId!.Value).Distinct();
        return await context.Tenants.AsNoTracking().Where(tenant => ids.Contains(tenant.Id))
            .OrderBy(tenant => tenant.Name).ThenBy(tenant => tenant.Id)
            .Select(tenant => new PlatformAuditOrganizationResponse(tenant.Id, tenant.Name)).ToArrayAsync(ct);
    }

    private IQueryable<AuditKey> Keys(ListPlatformAuditRequest request, DateTime? sinceUtc,
        IReadOnlyCollection<SecurityEventType>? eventTypes, bool includeSignIns)
    {
        var keys = FilteredEvents(request, sinceUtc, eventTypes)
            .Select(item => new AuditKey { Id = item.Id, OccurredAtUtc = item.OccurredAtUtc, IsSignIn = false });
        if (!includeSignIns) return keys;

        var byName = string.IsNullOrWhiteSpace(request.Search) ? null
            : context.Users.AsNoTracking().ApplySearch(request.Search, user => user.DisplayName).Select(user => user.Id);
        var signIns = context.LoginAudits.AsNoTracking();
        if (sinceUtc is { } from) signIns = signIns.Where(item => item.OccurredAtUtc >= from);
        if (request.ToUtc is { } end) signIns = signIns.Where(item => item.OccurredAtUtc < end);
        if (request.AccountId is { } account) signIns = signIns.Where(item => item.UserId == account);
        if (byName is not null) signIns = signIns.Where(item => item.UserId != null && byName.Contains(item.UserId.Value));
        return keys.Concat(signIns.Select(item => new AuditKey { Id = item.Id, OccurredAtUtc = item.OccurredAtUtc, IsSignIn = true }));
    }

    private IQueryable<SecurityEvent> FilteredEvents(ListPlatformAuditRequest request, DateTime? sinceUtc,
        IReadOnlyCollection<SecurityEventType>? eventTypes)
    {
        var byName = string.IsNullOrWhiteSpace(request.Search) ? null
            : context.Users.AsNoTracking().ApplySearch(request.Search, user => user.DisplayName).Select(user => user.Id);

        var events = context.SecurityEvents.AsNoTracking();
        if (eventTypes is not null) events = events.Where(item => eventTypes.Contains(item.Type));
        if (sinceUtc is { } since) events = events.Where(item => item.OccurredAtUtc >= since);
        if (request.ToUtc is { } end) events = events.Where(item => item.OccurredAtUtc < end);
        if (request.TenantId is { } tenantId) events = events.Where(item => item.TargetTenantId == tenantId);
        if (request.AccountId is { } accountId)
            events = events.Where(item => item.TargetAccountId == accountId || item.ActorId == accountId);
        if (byName is not null)
        {
            var byReason = context.SecurityEvents.AsNoTracking().ApplySearch(request.Search, item => item.Reason)
                .Select(item => item.Id);
            var byTenant = context.Tenants.AsNoTracking().ApplySearch(request.Search, tenant => tenant.Name)
                .Select(tenant => tenant.Id);
            events = events.Where(item => byReason.Contains(item.Id) || byName.Contains(item.ActorId)
                || item.TargetAccountId != null && byName.Contains(item.TargetAccountId.Value)
                || item.TargetTenantId != null && byTenant.Contains(item.TargetTenantId.Value));
        }

        return events;
    }

    private async Task<Dictionary<Guid, PlatformAuditRow>> LoadAsync(Guid[] eventIds, Guid[] signInIds, CancellationToken ct)
    {
        var events = await (
            from item in context.SecurityEvents.AsNoTracking()
            where eventIds.Contains(item.Id)
            select new
            {
                item.Id, item.OccurredAtUtc, item.Type, item.ActorKind, item.ActorId, item.TargetAccountId,
                item.TargetTenantId, item.Reason, item.MaskedIpAddress, item.Changes,
                Actor = context.Users.Where(user => user.Id == item.ActorId)
                    .Select(user => new AccountData(user.DisplayName, user.Email, user.Status, user.IsPlatformOperator)).FirstOrDefault(),
                Target = context.Users.Where(user => user.Id == item.TargetAccountId)
                    .Select(user => new AccountData(user.DisplayName, user.Email, user.Status, user.IsPlatformOperator)).FirstOrDefault(),
                TenantName = context.Tenants.Where(tenant => tenant.Id == item.TargetTenantId).Select(tenant => tenant.Name).FirstOrDefault(),
            }).ToArrayAsync(ct);
        var signIns = signInIds.Length == 0 ? [] : await (
            from item in context.LoginAudits.AsNoTracking()
            where signInIds.Contains(item.Id)
            select new
            {
                item.Id, item.OccurredAtUtc, item.Method, item.Succeeded, item.FailureCode, item.UserId,
                Account = context.Users.Where(user => user.Id == item.UserId)
                    .Select(user => new AccountData(user.DisplayName, user.Email, user.Status, user.IsPlatformOperator)).FirstOrDefault(),
            }).ToArrayAsync(ct);

        var rows = new Dictionary<Guid, PlatformAuditRow>();
        foreach (var item in events)
            rows[item.Id] = new PlatformAuditRow(item.Id, item.OccurredAtUtc, item.Type, null, false, null, item.ActorKind,
                ToAccount(item.Actor), ToAccount(item.Target), item.TargetAccountId, item.TargetTenantId, item.TenantName,
                item.Reason, item.MaskedIpAddress, item.Changes);
        foreach (var item in signIns)
            rows[item.Id] = new PlatformAuditRow(item.Id, item.OccurredAtUtc, null, item.Method, item.Succeeded,
                item.FailureCode, AuditActorKind.User, ToAccount(item.Account), ToAccount(item.Account), item.UserId, null,
                null, null, null);
        return rows;
    }

    private static PlatformAuditAccount? ToAccount(AccountData? account) => account is null ? null
        : new PlatformAuditAccount(account.DisplayName,
            account.Email is { } email && Email.Create(email) is { IsSuccess: true } valid ? valid.Value : null,
            account.Status == UserStatus.Deleted, account.IsPlatformOperator);

    private sealed record AccountData(string? DisplayName, string? Email, UserStatus Status, bool IsPlatformOperator);

    private sealed class AuditKey
    {
        public Guid Id { get; init; }
        public DateTime OccurredAtUtc { get; init; }
        public bool IsSignIn { get; init; }
    }
}
