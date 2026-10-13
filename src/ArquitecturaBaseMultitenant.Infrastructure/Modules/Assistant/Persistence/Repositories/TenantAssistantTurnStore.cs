using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class TenantAssistantTurnStore(ApplicationDbContext db, ITenantContext tenant, IAssistantConversationRepository conversations) : AssistantTurnStore
{
    public override bool Handles(Access access) => access is Access.Consumer or Access.Business;
    private IQueryable<AssistantTurn> Turns(Guid owner, Access access) => db.Set<AssistantTurn>().Where(row => row.TenantId == tenant.RequiredTenantId && db.Set<AssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.OwnerUserId == owner && parent.TenantId == row.TenantId && parent.Access == access));
    private IQueryable<AssistantMessage> Messages(Guid owner, Access access, Guid conversationId) => db.Set<AssistantMessage>().Where(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId && db.Set<AssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.OwnerUserId == owner && parent.TenantId == row.TenantId && parent.Access == access));
    public override async Task<AssistantTurnBase?> GetAsync(Guid owner, Access access, Guid id, bool forUpdate, CancellationToken ct) => await (forUpdate ? Turns(owner, access) : Turns(owner, access).AsNoTracking()).SingleOrDefaultAsync(row => row.Id == id, ct);
    public override Task<Guid?> FindActiveAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) =>
        Turns(owner, access).AsNoTracking().Where(row => row.ConversationId == conversationId && db.Set<AssistantTurnWork>().Any(work => work.Id == row.Id && (work.Status == AssistantTurnStatus.Pending || work.Status == AssistantTurnStatus.Running))).Select(row => (Guid?)row.Id).SingleOrDefaultAsync(ct);
    public override async Task<AssistantConversationBase?> GetConversationAsync(Guid owner, Access access, Guid id, uint? version, CancellationToken ct) => await conversations.GetByIdAsync(owner, access, id, version, ct);
    public override async Task<AssistantMessageBase?> GetMessageAsync(Guid owner, Access access, Guid conversationId, Guid messageId, CancellationToken ct) => await conversations.GetMessageAsync(owner, access, conversationId, messageId, null, ct);
    public override async Task<AssistantMessageBase?> GetMessageMetadataAsync(Guid owner, Access access, IAssistantTurn turn, CancellationToken ct)
    {
        var tracked = db.Set<AssistantMessage>().Local.SingleOrDefault(value => value.Id == turn.AssistantMessageId && value.TenantId == tenant.RequiredTenantId);
        if (tracked is not null && db.Entry(tracked).State == EntityState.Unchanged) db.Entry(tracked).State = EntityState.Detached;
        var accessValue = access.ToString();
        var row = await db.Set<AssistantMessage>().FromSqlInterpolated($"""
            SELECT m."TenantId", m."Id", m."ConversationId", m."Role", m."Status", m."CreatedAtUtc", m."SubjectUserIds", m."PrivacySubjectsKnown", m.xmin,
              NULL::text AS "Text", NULL::boolean AS "PositiveFeedback", NULL::text AS "ToolKey",
              NULL::bigint AS "ToolDurationMilliseconds", NULL::text AS "ToolState", NULL::text AS "LinksJson"
            FROM tenant."AssistantMessages" m
            JOIN tenant."AssistantConversations" c ON c."TenantId" = m."TenantId" AND c."Id" = m."ConversationId"
            WHERE m."Id" = {turn.AssistantMessageId} AND m."ConversationId" = {turn.ConversationId}
              AND m."TenantId" = {tenant.RequiredTenantId} AND c."OwnerUserId" = {owner} AND c."Access" = {accessValue}
            """).SingleOrDefaultAsync(ct);
        return row;
    }
    public override async Task<AssistantMessageBase[]> ListMessagesAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var query = Messages(owner, access, conversationId).AsNoTracking().Where(row => ids == null || ids.Contains(row.Id)).OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id);
        return await query.ToArrayAsync(ct);
    }
    public override async Task<AssistantHistoryMessageRow[]> FindHistoryPrivacyAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) => await Messages(owner, access, conversationId).AsNoTracking().OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).Select(row => new AssistantHistoryMessageRow(row.Id, row.Role, row.PrivacySubjectsKnown, row.SubjectUserIds)).ToArrayAsync(ct);
    public override async Task<AssistantStoredAttachmentRow[]> ListAttachmentsAsync(Guid conversationId, CancellationToken ct) => await db.Set<AssistantAttachment>().AsNoTracking().Where(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId && row.MessageId != null).Select(row => new AssistantStoredAttachmentRow(row.Id, row.MessageId, row.FileName, row.ContentType, row.Size, row.Version)).ToArrayAsync(ct);
    public override Task<int> CountUnboundAttachmentsAsync(Guid conversationId, IReadOnlyList<Guid> ids, CancellationToken ct) => db.Set<AssistantAttachment>().CountAsync(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId && row.MessageId == null && ids.Contains(row.Id), ct);
    public override AssistantTurnBase CreateTurn(Guid conversationId, DateTime nowUtc) => AssistantTurn.Create(conversationId, nowUtc);
    public override AssistantMessageBase CreateMessage(Guid conversationId, string role, string status, string? text, DateTime nowUtc) => AssistantMessage.Create(conversationId, role, status, text, nowUtc);
    public override async Task<AssistantDurableOperationBase?> GetBindingAsync(Guid owner, Access access, Guid id, CancellationToken ct) => await db.Set<AssistantDurableOperation>().SingleOrDefaultAsync(row => row.Id == id && row.OwnerUserId == owner && row.Access == access && row.TenantId == tenant.RequiredTenantId, ct);
    public override async Task<AssistantTurnBase?> GetBindingTurnAsync(Guid bindingId, Guid conversationId, CancellationToken ct) => await db.Set<AssistantTurn>().SingleOrDefaultAsync(row => row.BindingId == bindingId && row.ConversationId == conversationId && row.TenantId == tenant.RequiredTenantId, ct);
    public override Task<Guid[]> ListTurnIdsAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) => Turns(owner, access).Where(row => row.ConversationId == conversationId).Select(row => row.Id).ToArrayAsync(ct);
}
