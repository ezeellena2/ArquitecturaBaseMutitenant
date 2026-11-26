using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;

internal sealed class AssistantConversationReader(ApplicationDbContext context, ITenantContext tenant) : IAssistantConversationReader
{
    // 0064 §6: una conversación con un mensaje que esta versión no sabe leer (AssistantKnownMessages) sale sin título ni
    // mensajes, su GET y su exportación son 409, no recibe un turno y se puede borrar.
    private static readonly string[] KnownRoles = AssistantKnownMessages.Roles;
    private static readonly AssistantPersistedMessageStatus[] KnownStatuses = AssistantKnownMessages.Statuses;

    public async Task<AssistantConversationSummaryResponse?> FindByIdAsync(Guid ownerUserId, Access access, Guid conversationId, CancellationToken ct)
    {
        Validate(ownerUserId, access);
        var row = await Conversations(ownerUserId, access, null).Where(value => value.Id == conversationId).SingleOrDefaultAsync(ct);
        return row is null ? null : Summary(row);
    }

    public async Task<CursorResult<AssistantConversationSummaryResponse>> ListAsync(Guid ownerUserId, Access access, AssistantConversationListRequest request, CancellationToken ct)
    {
        Validate(ownerUserId, access);
        var tenantId = access == Access.Platform ? Guid.Empty : tenant.RequiredTenantId;
        var page = await AssistantCursorPagination.ToCursorResultAsync(Conversations(ownerUserId, access, request.Saved), request.Limit ?? AssistantTechnicalLimits.QuickRows, request.Cursor,
            $"conversations:{tenantId:N}:{ownerUserId:N}:{access}:{request.Saved?.ToString() ?? "all"}", false,
            value => value.LastActivityAtUtc, value => value.Id, ct);
        return new CursorResult<AssistantConversationSummaryResponse>([.. page.Items.Select(Summary)], page.NextCursor, page.HasMore);
    }

    private IQueryable<ConversationRow> Conversations(Guid ownerUserId, Access access, bool? saved)
    {
        if (access == Access.Platform)
            return context.Set<PlatformAssistantConversation>().AsNoTracking().Where(value => value.OwnerUserId == ownerUserId && (saved == null || value.IsSaved == saved))
                .Select(value => new ConversationRow
                {
                    Id = value.Id, Title = value.ManualTitle ?? value.Activity.AutomaticTitle, IsSaved = value.IsSaved, Version = value.Version,
                    CreatedAtUtc = value.CreatedAtUtc, LastActivityAtUtc = value.Activity.LastActivityAtUtc,
                    Available = !context.Set<PlatformAssistantMessage>().Any(message => message.ConversationId == value.Id
                        && (!KnownRoles.Contains(message.Role) || !KnownStatuses.Contains(message.Status))),
                });
        var tenantId = tenant.RequiredTenantId;
        return context.Set<AssistantConversation>().AsNoTracking().Where(value => value.TenantId == tenantId && value.OwnerUserId == ownerUserId && value.Access == access && (saved == null || value.IsSaved == saved))
            .Select(value => new ConversationRow
            {
                Id = value.Id, Title = value.ManualTitle ?? value.Activity.AutomaticTitle, IsSaved = value.IsSaved, Version = value.Version,
                CreatedAtUtc = value.CreatedAtUtc, LastActivityAtUtc = value.Activity.LastActivityAtUtc,
                Available = !context.Set<AssistantMessage>().Any(message => message.TenantId == value.TenantId && message.ConversationId == value.Id
                    && (!KnownRoles.Contains(message.Role) || !KnownStatuses.Contains(message.Status))),
            });
    }

    private static AssistantConversationSummaryResponse Summary(ConversationRow row) =>
        new(row.Id, row.Available ? row.Title : null, row.IsSaved, row.Version, row.CreatedAtUtc, row.LastActivityAtUtc) { Available = row.Available };

    // Con inicializadores y no con constructor: el cursor ordena por sus propiedades después de proyectar.
    private sealed class ConversationRow
    {
        public Guid Id { get; init; }
        public string? Title { get; init; }
        public bool IsSaved { get; init; }
        public uint Version { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime LastActivityAtUtc { get; init; }
        public bool Available { get; init; }
    }

    public async Task<CursorResult<AssistantMessageResponse>> ListMessagesAsync(Guid ownerUserId, Access access, Guid conversationId, AssistantMessageListRequest request, CancellationToken ct)
    {
        Validate(ownerUserId, access);
        var tenantId = access == Access.Platform ? Guid.Empty : tenant.RequiredTenantId;
        var query = access == Access.Platform
            ? context.Set<PlatformAssistantMessage>().AsNoTracking()
                .Where(value => value.ConversationId == conversationId && context.Set<PlatformAssistantConversation>().Any(conversation => conversation.Id == value.ConversationId && conversation.OwnerUserId == ownerUserId))
                .Select(value => new { value.Id, value.Role, value.Status, value.Text, value.CreatedAtUtc, value.Version, value.PositiveFeedback, value.LinksJson })
            : context.Set<AssistantMessage>().AsNoTracking().Where(value => value.TenantId == tenantId && value.ConversationId == conversationId
                && context.Set<AssistantConversation>().Any(conversation => conversation.TenantId == value.TenantId && conversation.Id == value.ConversationId && conversation.OwnerUserId == ownerUserId && conversation.Access == access))
                .Select(value => new { value.Id, value.Role, value.Status, value.Text, value.CreatedAtUtc, value.Version, value.PositiveFeedback, value.LinksJson });
        var page = await AssistantCursorPagination.ToCursorResultAsync(query, request.Limit ?? 50, request.Cursor,
            $"messages:{tenantId:N}:{ownerUserId:N}:{access}:{conversationId:N}", true,
            value => value.CreatedAtUtc, value => value.Id, ct);
        var ids = page.Items.Select(row => row.Id).ToArray();
        var attachmentRows = access == Access.Platform
            ? await context.Set<PlatformAssistantAttachment>().AsNoTracking().Where(row => row.ConversationId == conversationId && row.MessageId != null && ids.Contains(row.MessageId.Value))
                .Select(row => new { MessageId = row.MessageId!.Value, row.Id, row.FileName, row.ContentType, row.Size, row.Version }).ToArrayAsync(ct)
            : await context.Set<AssistantAttachment>().AsNoTracking().Where(row => row.TenantId == tenantId && row.ConversationId == conversationId && row.MessageId != null && ids.Contains(row.MessageId.Value))
                .Select(row => new { MessageId = row.MessageId!.Value, row.Id, row.FileName, row.ContentType, row.Size, row.Version }).ToArrayAsync(ct);
        var attachments = attachmentRows.GroupBy(row => row.MessageId).ToDictionary(group => group.Key,
            group => group.Select(row => new AssistantAttachmentSummaryResponse(row.Id, row.FileName, row.ContentType, row.Size, row.Version)).ToArray());
        // ADR 0068 §8: la propuesta del turno, con su estado guardado; el GET de la propuesta calcula lo demás.
        var proposalOf = access == Access.Platform ? new Dictionary<Guid, AssistantMessageProposalResponse>()
            : await AssistantMessageProposalQuery.ListAsync(context, tenantId, conversationId, ids, ct);
        return new CursorResult<AssistantMessageResponse>([.. page.Items.Select(value => new AssistantMessageResponse(value.Id,
            Enum.Parse<AssistantMessageRole>(value.Role), value.Text ?? "", Enum.Parse<AssistantMessageStatus>(value.Status.ToString()), value.CreatedAtUtc, value.Version,
            attachments.GetValueOrDefault(value.Id) ?? [], value.LinksJson is null ? [] : JsonSerializer.Deserialize<AssistantLinkResponse[]>(value.LinksJson)!,
            value.PositiveFeedback == null ? null : value.PositiveFeedback.Value ? AssistantRating.Up : AssistantRating.Down, proposalOf.GetValueOrDefault(value.Id)))],
            page.NextCursor, page.HasMore);
    }

    public Task<bool> ExistsAttachmentAsync(Guid ownerUserId, Access access, Guid conversationId, Guid attachmentId, CancellationToken ct)
    {
        Validate(ownerUserId, access);
        if (access == Access.Platform)
            return context.Set<PlatformAssistantAttachment>().AsNoTracking().AnyAsync(value => value.Id == attachmentId && value.ConversationId == conversationId
                && context.Set<PlatformAssistantConversation>().Any(conversation => conversation.Id == value.ConversationId && conversation.OwnerUserId == ownerUserId), ct);
        var tenantId = tenant.RequiredTenantId;
        return context.Set<AssistantAttachment>().AsNoTracking().AnyAsync(value => value.Id == attachmentId && value.TenantId == tenantId && value.ConversationId == conversationId
            && context.Set<AssistantConversation>().Any(conversation => conversation.TenantId == value.TenantId && conversation.Id == value.ConversationId && conversation.OwnerUserId == ownerUserId && conversation.Access == access), ct);
    }

    private static void Validate(Guid ownerUserId, Access access)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        if (!Enum.IsDefined(access)) throw new ArgumentOutOfRangeException(nameof(access));
    }
}
