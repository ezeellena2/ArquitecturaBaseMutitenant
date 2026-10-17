using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>
/// Las difusiones con sus resultados. El estado de un destinatario que salió se lee del envío: fallido si el outbox no lo
/// pudo mandar; si no, lo último que informó Meta en la fila «accepted» (enviado, entregado, leído o fallido).
/// </summary>
internal sealed class WhatsAppBroadcastReader(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppBroadcastReader
{
    public async Task<PagedResult<WhatsAppBroadcastResponse>> ListAsync(WhatsAppBroadcastListRequest request, CancellationToken ct)
    {
        var broadcasts = Broadcasts();
        if (request.Status is { } status) broadcasts = broadcasts.Where(broadcast => broadcast.Status == status);
        var page = await broadcasts.ApplySearch(request.Search, broadcast => broadcast.Name)
            .OrderByDescending(broadcast => broadcast.SentAtUtc ?? broadcast.ScheduledAtUtc ?? broadcast.CreatedAtUtc).ThenByDescending(broadcast => broadcast.Id)
            .ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppBroadcastResponse>(await ResponsesAsync(page.Items, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public Task<int> CountScheduledAsync(CancellationToken ct) =>
        Broadcasts().CountAsync(broadcast => broadcast.Status == WhatsAppBroadcastStatus.Scheduled, ct);

    public async Task<WhatsAppBroadcastResponse?> FindAsync(Guid id, CancellationToken ct) =>
        await Broadcasts().SingleOrDefaultAsync(broadcast => broadcast.Id == id, ct) is { } broadcast ? (await ResponsesAsync([broadcast], ct))[0] : null;

    public async Task<PagedResult<WhatsAppRecipientResponse>> ListRecipientsAsync(Guid broadcastId, WhatsAppBroadcastDetailRequest request, CancellationToken ct)
    {
        var recipients = Recipients().Where(recipient => recipient.BroadcastId == broadcastId);
        if (request.Status is { } status) recipients = recipients.Where(recipient => recipient.Status == status);
        var page = await recipients.ApplySearch(request.Search, recipient => recipient.ContactName, recipient => recipient.WaId)
            .OrderByDescending(recipient => recipient.ChangedAtUtc).ThenBy(recipient => recipient.Id).ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppRecipientResponse>([.. page.Items.Select(recipient => new WhatsAppRecipientResponse(recipient.ContactId,
            recipient.ContactName, recipient.WaId, recipient.Status, recipient.FailureReason, recipient.ChangedAtUtc))], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PagedResult<WhatsAppContactBroadcastResponse>> ListForContactAsync(Guid contactId, PagedRequest request, CancellationToken ct)
    {
        var page = await (from recipient in Recipients().Where(recipient => recipient.ContactId == contactId && recipient.Status != WhatsAppRecipientStatus.Pending)
                          join broadcast in Broadcasts() on recipient.BroadcastId equals broadcast.Id
                          orderby recipient.SentAtUtc ?? recipient.ChangedAtUtc descending, recipient.Id descending
                          select new WhatsAppContactBroadcastResponse(broadcast.Id, broadcast.Name, recipient.SentAtUtc ?? recipient.ChangedAtUtc, recipient.Status))
            .ToPagedResultAsync(request, ct);
        return page;
    }

    public Task<WhatsAppDueBroadcast?> FindNextDueAsync(DateTime nowUtc, CancellationToken ct) =>
        Broadcasts().Where(broadcast => broadcast.Status == WhatsAppBroadcastStatus.Sending || broadcast.Status == WhatsAppBroadcastStatus.Scheduled
                && (broadcast.ScheduledAtUtc == null || broadcast.ScheduledAtUtc <= nowUtc))
            .OrderBy(broadcast => broadcast.StartedAtUtc ?? broadcast.ScheduledAtUtc ?? broadcast.CreatedAtUtc).ThenBy(broadcast => broadcast.Id)
            .Select(broadcast => new WhatsAppDueBroadcast(broadcast.Id, broadcast.TemplateId, EF.Property<List<string>>(broadcast, "_variables")))
            .FirstOrDefaultAsync(ct);

    public Task<int> CountContactsSentSinceAsync(DateTime sinceUtc, CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppBroadcastRecipient>().AsNoTracking()
            .Where(recipient => recipient.TenantId == tenantId && recipient.SentAtUtc >= sinceUtc).Select(recipient => recipient.CustomerId).Distinct().CountAsync(ct);
    }

    private async Task<IReadOnlyList<WhatsAppBroadcastResponse>> ResponsesAsync(IReadOnlyList<WhatsAppBroadcast> broadcasts, CancellationToken ct)
    {
        var ids = broadcasts.Select(broadcast => broadcast.Id).ToArray();
        var counts = (await Recipients().Where(recipient => ids.Contains(recipient.BroadcastId))
                .GroupBy(recipient => new { recipient.BroadcastId, recipient.Status })
                .Select(group => new { group.Key.BroadcastId, group.Key.Status, Count = group.Count() }).ToArrayAsync(ct))
            .ToLookup(row => row.BroadcastId);
        return [.. broadcasts.Select(broadcast =>
        {
            var byStatus = counts[broadcast.Id].ToDictionary(row => row.Status, row => row.Count);
            int Count(params WhatsAppRecipientStatus[] statuses) => statuses.Sum(status => byStatus.GetValueOrDefault(status));
            return new WhatsAppBroadcastResponse(broadcast.Id, broadcast.Name, new WhatsAppReferenceResponse(broadcast.TemplateId, broadcast.TemplateName),
                [.. broadcast.Variables], new WhatsAppAudienceResponse(broadcast.AudienceKind, broadcast.AudienceTag), broadcast.Recipients, broadcast.Status,
                broadcast.ScheduledAtUtc, broadcast.SentAtUtc,
                Count(WhatsAppRecipientStatus.Delivered, WhatsAppRecipientStatus.Read, WhatsAppRecipientStatus.Replied),
                Count(WhatsAppRecipientStatus.Read, WhatsAppRecipientStatus.Replied), Count(WhatsAppRecipientStatus.Replied),
                Count(WhatsAppRecipientStatus.Unsubscribed), Count(WhatsAppRecipientStatus.Failed), broadcast.EstimatedCost, broadcast.Version);
        })];
    }

    private IQueryable<WhatsAppBroadcast> Broadcasts()
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppBroadcast>().AsNoTracking().Where(broadcast => broadcast.TenantId == tenantId);
    }

    private IQueryable<RecipientProjection> Recipients()
    {
        var tenantId = tenant.RequiredTenantId;
        return from recipient in context.Set<WhatsAppBroadcastRecipient>().AsNoTracking().Where(recipient => recipient.TenantId == tenantId)
               from outbox in context.OutboxMessages.AsNoTracking().Where(outbox => outbox.Id == recipient.OutboxMessageId).DefaultIfEmpty()
               from accepted in context.Set<WhatsAppMessage>().AsNoTracking()
                   .Where(accepted => accepted.EventType == "accepted" && accepted.OutboxMessageId == recipient.OutboxMessageId).DefaultIfEmpty()
               from customer in context.Set<WhatsAppCustomer>().AsNoTracking()
                   .Where(customer => customer.TenantId == tenantId && customer.Id == recipient.CustomerId).DefaultIfEmpty()
               select new RecipientProjection
               {
                   Id = recipient.Id, BroadcastId = recipient.BroadcastId, ContactId = recipient.CustomerId,
                   ContactName = customer == null ? null : customer.Name, WaId = recipient.WaId,
                   Status = recipient.Status != WhatsAppRecipientStatus.Sent ? recipient.Status
                       : outbox != null && (outbox.Status == OutboxStatus.Failed || outbox.Status == OutboxStatus.Cancelled) ? WhatsAppRecipientStatus.Failed
                       : accepted == null ? WhatsAppRecipientStatus.Sent
                       : accepted.Status == WhatsAppMessageStatus.Read ? WhatsAppRecipientStatus.Read
                       : accepted.Status == WhatsAppMessageStatus.Delivered ? WhatsAppRecipientStatus.Delivered
                       : accepted.Status == WhatsAppMessageStatus.Failed ? WhatsAppRecipientStatus.Failed
                       : WhatsAppRecipientStatus.Sent,
                   FailureReason = recipient.FailureReason
                       ?? (outbox != null && (outbox.Status == OutboxStatus.Failed || outbox.Status == OutboxStatus.Cancelled) ? outbox.LastErrorCode : null),
                   ChangedAtUtc = recipient.Status == WhatsAppRecipientStatus.Sent && accepted != null && accepted.OccurredAtUtc > recipient.ChangedAtUtc
                       ? accepted.OccurredAtUtc : recipient.ChangedAtUtc,
                   SentAtUtc = recipient.SentAtUtc,
               };
    }

    private sealed class RecipientProjection
    {
        public Guid Id { get; init; }
        public Guid BroadcastId { get; init; }
        public Guid ContactId { get; init; }
        public string? ContactName { get; init; }
        public string WaId { get; init; } = string.Empty;
        public WhatsAppRecipientStatus Status { get; init; }
        public string? FailureReason { get; init; }
        public DateTime ChangedAtUtc { get; init; }
        public DateTime? SentAtUtc { get; init; }
    }
}
