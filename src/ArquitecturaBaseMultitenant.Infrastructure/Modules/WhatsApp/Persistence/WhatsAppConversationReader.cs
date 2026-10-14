using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>
/// Las bandejas de la organización del claim (con RLS) y las consultas de Plataforma. El estado de lo que salió se lee del
/// outbox (pendiente o fallido, con su código) y de la fila «accepted» de Meta (enviado, entregado, leído o fallido).
/// </summary>
internal sealed class WhatsAppConversationReader(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppConversationReader
{
    public async Task<PagedResult<WhatsAppConversationRow>> ListAsync(WhatsAppInbox? inbox, WhatsAppConversationListRequest request, Guid userId,
        CancellationToken ct)
    {
        var rows = Rows(inbox);
        if (request.TeamId is { } teamId) rows = rows.Where(row => row.TeamId == teamId);
        rows = request.State switch
        {
            WhatsAppConversationFilter.Untaken => rows.Where(row => row.AssigneeUserId == null),
            WhatsAppConversationFilter.Mine => rows.Where(row => row.AssigneeUserId == userId),
            _ => rows,
        };
        var page = await rows.ApplySearch(request.Search, row => row.ContactName, row => row.WaId)
            .OrderByDescending(row => row.LastMessageAtUtc).ThenByDescending(row => row.Id).ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppConversationRow>([.. page.Items.Select(Row)], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<WhatsAppConversationRow?> FindAsync(WhatsAppInbox? inbox, Guid id, CancellationToken ct) =>
        await Rows(inbox).Where(row => row.Id == id).SingleOrDefaultAsync(ct) is { } row ? Row(row) : null;

    public Task<int> CountAsync(WhatsAppInbox? inbox, bool untaken, CancellationToken ct)
    {
        if (inbox is not { } value)
            return context.Set<PlatformWhatsAppChat>().CountAsync(chat => !untaken || chat.AssigneeUserId == null, ct);
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppChat>().CountAsync(chat => chat.TenantId == tenantId && chat.Inbox == value
            && (!untaken || chat.AssigneeUserId == null), ct);
    }

    public Task<PagedResult<WhatsAppChatMessageResponse>> ListMessagesAsync(WhatsAppInbox? inbox, Guid chatId, PagedRequest request,
        CancellationToken ct)
    {
        if (inbox is null)
            return MessagesAsync(context.Set<PlatformWhatsAppChatMessage>().AsNoTracking().Where(message => message.ChatId == chatId), request, ct);
        var tenantId = tenant.RequiredTenantId;
        return MessagesAsync(context.Set<WhatsAppChatMessage>().AsNoTracking()
            .Where(message => message.TenantId == tenantId && message.ChatId == chatId), request, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountTakenAsync(WhatsAppInbox? inbox, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, int>();
        if (inbox is not { } value)
            return await context.Set<PlatformWhatsAppChat>().AsNoTracking()
                .Where(chat => chat.AssigneeUserId != null && userIds.Contains(chat.AssigneeUserId.Value))
                .GroupBy(chat => chat.AssigneeUserId!.Value).Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.Key, group => group.Count, ct);
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppChat>().AsNoTracking()
            .Where(chat => chat.TenantId == tenantId && chat.Inbox == value && chat.AssigneeUserId != null
                && userIds.Contains(chat.AssigneeUserId.Value))
            .GroupBy(chat => chat.AssigneeUserId!.Value).Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, ct);
    }

    private IQueryable<ConversationProjection> Rows(WhatsAppInbox? inbox)
    {
        var channels = context.Set<WhatsAppChannel>().AsNoTracking();
        if (inbox is null)
            return from chat in context.Set<PlatformWhatsAppChat>().AsNoTracking()
                   join channel in channels on chat.ChannelId equals channel.Id
                   from team in context.Set<PlatformWhatsAppTeam>().AsNoTracking().Where(team => team.Id == chat.TeamId).DefaultIfEmpty()
                   select new ConversationProjection
                   {
                       Id = chat.Id, Inbox = null, ContactId = null, ContactName = null, Tags = null, MemberUserId = null, WaId = chat.WaId,
                       LastMessagePreview = chat.LastMessagePreview, LastMessageAtUtc = chat.LastMessageAtUtc, AwaitingReply = chat.AwaitingReply,
                       TeamId = chat.TeamId, TeamName = team == null ? null : team.Name, AssigneeUserId = chat.AssigneeUserId,
                       AssigneeMemberId = null, LastInboundAtUtc = chat.LastInboundAtUtc, ChannelId = channel.Id,
                       ChannelNumber = channel.DisplayNumber, ChannelStatus = channel.Status, Version = chat.Version,
                   };
        var tenantId = tenant.RequiredTenantId;
        var box = inbox.Value;
        return from chat in context.Set<WhatsAppChat>().AsNoTracking().Where(chat => chat.TenantId == tenantId && chat.Inbox == box)
               join channel in channels on chat.ChannelId equals channel.Id
               from customer in context.Set<WhatsAppCustomer>().AsNoTracking()
                   .Where(customer => customer.TenantId == tenantId && customer.Id == chat.CustomerId).DefaultIfEmpty()
               from team in context.Set<WhatsAppTeam>().AsNoTracking()
                   .Where(team => team.TenantId == tenantId && team.Id == chat.TeamId).DefaultIfEmpty()
               from member in context.Members.AsNoTracking()
                   .Where(member => member.TenantId == tenantId && member.UserId == chat.AssigneeUserId && member.Status == MemberStatus.Active)
                   .DefaultIfEmpty()
               select new ConversationProjection
               {
                   Id = chat.Id, Inbox = chat.Inbox, ContactId = chat.CustomerId, ContactName = customer == null ? null : customer.Name,
                   Tags = customer == null ? null : customer.Tags, MemberUserId = chat.MemberUserId, WaId = chat.WaId,
                   LastMessagePreview = chat.LastMessagePreview, LastMessageAtUtc = chat.LastMessageAtUtc, AwaitingReply = chat.AwaitingReply,
                   TeamId = chat.TeamId, TeamName = team == null ? null : team.Name, AssigneeUserId = chat.AssigneeUserId,
                   AssigneeMemberId = member == null ? null : member.Id, LastInboundAtUtc = chat.LastInboundAtUtc, ChannelId = channel.Id,
                   ChannelNumber = channel.DisplayNumber, ChannelStatus = channel.Status, Version = chat.Version,
               };
    }

    private static WhatsAppConversationRow Row(ConversationProjection row) => new(row.Id, row.Inbox, row.ContactId, row.ContactName,
        row.Tags is null ? [] : [.. row.Tags], row.MemberUserId, row.WaId, row.LastMessagePreview, row.LastMessageAtUtc, row.AwaitingReply,
        row.TeamId, row.TeamName, row.AssigneeUserId, row.AssigneeMemberId, row.LastInboundAtUtc, row.ChannelId, row.ChannelNumber,
        row.ChannelStatus, row.Version);

    private async Task<PagedResult<WhatsAppChatMessageResponse>> MessagesAsync<T>(IQueryable<T> messages, PagedRequest request, CancellationToken ct)
        where T : WhatsAppChatMessageBase
    {
        var page = await (from message in messages
                          from outbox in context.OutboxMessages.AsNoTracking().Where(outbox => outbox.Id == message.OutboxMessageId).DefaultIfEmpty()
                          from accepted in context.Set<WhatsAppMessage>().AsNoTracking()
                              .Where(accepted => accepted.EventType == "accepted" && accepted.OutboxMessageId == message.OutboxMessageId)
                              .DefaultIfEmpty()
                          orderby message.CreatedAtUtc descending, message.Id descending
                          select new MessageProjection
                          {
                              Id = message.Id, Kind = message.Kind, Direction = message.Direction, Author = message.AuthorName, Text = message.Text,
                              MediaKind = message.MediaKind, Status = message.Status, FailureReason = message.FailureReason,
                              CreatedAtUtc = message.CreatedAtUtc, OutboxStatus = outbox == null ? null : outbox.Status,
                              OutboxError = outbox == null ? null : outbox.LastErrorCode, Delivery = accepted == null ? null : accepted.Status,
                          }).ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppChatMessageResponse>([.. page.Items.Select(Message)], page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>Lo que salió toma el estado más avanzado que se conoce; un rechazo del outbox lleva su código seguro.</summary>
    private static WhatsAppChatMessageResponse Message(MessageProjection row)
    {
        var (status, reason) = row.Direction == WhatsAppMessageDirection.Inbound || row.Kind == WhatsAppChatMessageKind.Note
            ? (row.Status, row.FailureReason)
            : row.OutboxStatus is OutboxStatus.Failed or OutboxStatus.Cancelled ? (WhatsAppChatMessageStatus.Failed, row.OutboxError)
            : row.Delivery switch
            {
                WhatsAppMessageStatus.Read => (WhatsAppChatMessageStatus.Read, null),
                WhatsAppMessageStatus.Delivered => (WhatsAppChatMessageStatus.Delivered, null),
                WhatsAppMessageStatus.Failed => (WhatsAppChatMessageStatus.Failed, row.FailureReason),
                not null => (WhatsAppChatMessageStatus.Sent, null),
                null => row.OutboxStatus == OutboxStatus.Sent ? (WhatsAppChatMessageStatus.Sent, null) : (row.Status, row.FailureReason),
            };
        return new(row.Id, row.Kind, row.Direction, row.Author, row.Text, row.MediaKind, status, reason, row.CreatedAtUtc);
    }

    private sealed class ConversationProjection
    {
        public Guid Id { get; init; }
        public WhatsAppInbox? Inbox { get; init; }
        public Guid? ContactId { get; init; }
        public string? ContactName { get; init; }
        public IReadOnlyCollection<string>? Tags { get; init; }
        public Guid? MemberUserId { get; init; }
        public string WaId { get; init; } = string.Empty;
        public string? LastMessagePreview { get; init; }
        public DateTime LastMessageAtUtc { get; init; }
        public bool AwaitingReply { get; init; }
        public Guid? TeamId { get; init; }
        public string? TeamName { get; init; }
        public Guid? AssigneeUserId { get; init; }
        public Guid? AssigneeMemberId { get; init; }
        public DateTime? LastInboundAtUtc { get; init; }
        public Guid ChannelId { get; init; }
        public string? ChannelNumber { get; init; }
        public WhatsAppNumberStatus ChannelStatus { get; init; }
        public uint Version { get; init; }
    }

    private sealed class MessageProjection
    {
        public Guid Id { get; init; }
        public WhatsAppChatMessageKind Kind { get; init; }
        public WhatsAppMessageDirection Direction { get; init; }
        public string? Author { get; init; }
        public string? Text { get; init; }
        public WhatsAppMediaKind? MediaKind { get; init; }
        public WhatsAppChatMessageStatus Status { get; init; }
        public string? FailureReason { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public OutboxStatus? OutboxStatus { get; init; }
        public string? OutboxError { get; init; }
        public WhatsAppMessageStatus? Delivery { get; init; }
    }
}
