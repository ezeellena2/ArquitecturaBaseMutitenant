using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class PlatformAssistantTurnStore(ApplicationDbContext db, IPlatformAssistantConversationRepository conversations) : AssistantTurnStore
{
    public override bool Handles(Access access) => access == Access.Platform;
    private IQueryable<PlatformAssistantTurn> Turns(Guid owner, Access access) => db.Set<PlatformAssistantTurn>().Where(row => db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.OwnerUserId == owner));
    private IQueryable<PlatformAssistantMessage> Messages(Guid owner, Access access, Guid conversationId) => db.Set<PlatformAssistantMessage>().Where(row => row.ConversationId == conversationId && db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.OwnerUserId == owner));
    public override async Task<AssistantTurnBase?> GetAsync(Guid owner, Access access, Guid id, bool forUpdate, CancellationToken ct) => await (forUpdate ? Turns(owner, access) : Turns(owner, access).AsNoTracking()).SingleOrDefaultAsync(row => row.Id == id, ct);
    public override Task<Guid?> FindActiveAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) =>
        Turns(owner, access).AsNoTracking().Where(row => row.ConversationId == conversationId && db.Set<AssistantTurnWork>().Any(work => work.Id == row.Id && (work.Status == AssistantTurnStatus.Pending || work.Status == AssistantTurnStatus.Running))).Select(row => (Guid?)row.Id).SingleOrDefaultAsync(ct);
    public override async Task<AssistantConversationBase?> GetConversationAsync(Guid owner, Access access, Guid id, uint? version, CancellationToken ct) => await conversations.GetByIdAsync(owner, id, version, ct);
    public override async Task<AssistantMessageBase?> GetMessageAsync(Guid owner, Access access, Guid conversationId, Guid messageId, CancellationToken ct) => await conversations.GetMessageAsync(owner, conversationId, messageId, null, ct);
    public override async Task<AssistantMessageBase?> GetMessageMetadataAsync(Guid owner, Access access, IAssistantTurn turn, CancellationToken ct)
    {
        var tracked = db.Set<PlatformAssistantMessage>().Local.SingleOrDefault(value => value.Id == turn.AssistantMessageId);
        if (tracked is not null && db.Entry(tracked).State == EntityState.Unchanged) db.Entry(tracked).State = EntityState.Detached;
        var row = await db.Set<PlatformAssistantMessage>().FromSqlInterpolated($"""
            SELECT m."Id", m."ConversationId", m."Role", m."Status", m."CreatedAtUtc", m."SubjectUserIds", m."PrivacySubjectsKnown", m.xmin,
              NULL::text AS "Text", NULL::boolean AS "PositiveFeedback", NULL::text AS "ToolKey",
              NULL::bigint AS "ToolDurationMilliseconds", NULL::text AS "ToolState", NULL::text AS "LinksJson"
            FROM platform."PlatformAssistantMessages" m
            JOIN platform."PlatformAssistantConversations" c ON c."Id" = m."ConversationId"
            WHERE m."Id" = {turn.AssistantMessageId} AND m."ConversationId" = {turn.ConversationId} AND c."OwnerUserId" = {owner}
            """).SingleOrDefaultAsync(ct);
        return row;
    }
    public override async Task<AssistantMessageBase[]> ListMessagesAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var query = Messages(owner, access, conversationId).AsNoTracking().Where(row => ids == null || ids.Contains(row.Id)).OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id);
        return await query.ToArrayAsync(ct);
    }
    public override async Task<AssistantHistoryMessageRow[]> FindHistoryPrivacyAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) => await Messages(owner, access, conversationId).AsNoTracking().OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).Select(row => new AssistantHistoryMessageRow(row.Id, row.Role, row.PrivacySubjectsKnown, row.SubjectUserIds)).ToArrayAsync(ct);
    public override async Task<AssistantStoredAttachmentRow[]> ListAttachmentsAsync(Guid conversationId, CancellationToken ct) => await db.Set<PlatformAssistantAttachment>().AsNoTracking().Where(row => row.ConversationId == conversationId && row.MessageId != null).Select(row => new AssistantStoredAttachmentRow(row.Id, row.MessageId, row.FileName, row.ContentType, row.Size, row.Version)).ToArrayAsync(ct);
    public override Task<int> CountUnboundAttachmentsAsync(Guid conversationId, IReadOnlyList<Guid> ids, CancellationToken ct) => db.Set<PlatformAssistantAttachment>().CountAsync(row => row.ConversationId == conversationId && row.MessageId == null && ids.Contains(row.Id), ct);
    public override AssistantTurnBase CreateTurn(Guid conversationId, DateTime nowUtc) => PlatformAssistantTurn.Create(conversationId, nowUtc);
    public override AssistantMessageBase CreateMessage(Guid conversationId, string role, string status, string? text, DateTime nowUtc) => PlatformAssistantMessage.Create(conversationId, role, status, text, nowUtc);
    public override async Task<AssistantDurableOperationBase?> GetBindingAsync(Guid owner, Access access, Guid id, CancellationToken ct) => await db.Set<PlatformAssistantDurableOperation>().SingleOrDefaultAsync(row => row.Id == id && row.OwnerUserId == owner, ct);
    public override async Task<AssistantTurnBase?> GetBindingTurnAsync(Guid bindingId, Guid conversationId, CancellationToken ct) => await db.Set<PlatformAssistantTurn>().SingleOrDefaultAsync(row => row.BindingId == bindingId && row.ConversationId == conversationId, ct);
    public override Task<Guid[]> ListTurnIdsAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) => Turns(owner, access).Where(row => row.ConversationId == conversationId).Select(row => row.Id).ToArrayAsync(ct);
}
