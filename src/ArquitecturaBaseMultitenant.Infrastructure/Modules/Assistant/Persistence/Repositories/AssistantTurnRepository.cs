using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantTurnRepository(ApplicationDbContext db, ITenantContext tenant,
    IAssistantConversationRepository conversations, IPlatformAssistantConversationRepository platform, IAssistantAttachmentRepository attachments) : IAssistantTurnRepository
{
    public Task BindAttachmentsAsync(Guid owner, Access access, Guid conversationId, Guid messageId, IReadOnlyList<Guid> ids, CancellationToken ct) =>
        attachments.BindAsync(owner, access, conversationId, messageId, ids, ct);
    public async Task<IReadOnlyList<AssistantTurnDiscovery>> ListReadyAsync(DateTime nowUtc, CancellationToken ct) =>
        await db.Set<AssistantTurnWork>().AsNoTracking()
            .Where(row => row.Status == "Pending" || row.Status == "Running" && row.LeaseExpiresAtUtc <= nowUtc)
            .OrderBy(row => row.CreatedAtUtc).Take(32)
            .Select(row => new AssistantTurnDiscovery(row.Id, new ExecutionActor(row.OwnerUserId, row.Access, row.ScopeTenantId, row.Culture, row.TimeZoneId)
                { TenantKind = row.TenantKind, SessionStartedAtUtc = row.SessionStartedAtUtc })).ToArrayAsync(ct);

    public async Task<AssistantTurnWork?> GetWorkAsync(Guid id, bool forUpdate, CancellationToken ct)
    {
        if (!forUpdate) return await db.Set<AssistantTurnWork>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct);
        db.RequireTransaction();
        var row = await db.Set<AssistantTurnWork>().FromSqlInterpolated($"""
            SELECT * FROM platform."AssistantTurnWorks" WHERE "Id" = {id} FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(ct);
        if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
        return row;
    }

    public async Task<IAssistantTurn?> GetAsync(Guid owner, Access access, Guid id, bool forUpdate, CancellationToken ct)
    {
        if (access == Access.Platform)
        {
            var query = db.Set<PlatformAssistantTurn>().Where(row => row.Id == id &&
                db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.OwnerUserId == owner));
            var row = await (forUpdate ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
            if (forUpdate && row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
            return row;
        }
        else
        {
            var query = db.Set<AssistantTurn>().Where(row => row.Id == id && row.TenantId == tenant.RequiredTenantId &&
                db.Set<AssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.TenantId == row.TenantId && parent.OwnerUserId == owner && parent.Access == access));
            var row = await (forUpdate ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
            if (forUpdate && row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
            return row;
        }
    }
    public Task<Guid?> FindActiveAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) =>
        access == Access.Platform
            ? db.Set<PlatformAssistantTurn>().AsNoTracking().Where(row => row.ConversationId == conversationId &&
                db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == conversationId && parent.OwnerUserId == owner) &&
                db.Set<AssistantTurnWork>().Any(work => work.Id == row.Id && (work.Status == "Pending" || work.Status == "Running")))
                .Select(row => (Guid?)row.Id).SingleOrDefaultAsync(ct)
            : db.Set<AssistantTurn>().AsNoTracking().Where(row => row.ConversationId == conversationId && row.TenantId == tenant.RequiredTenantId &&
                db.Set<AssistantConversation>().Any(parent => parent.Id == conversationId && parent.TenantId == row.TenantId && parent.OwnerUserId == owner && parent.Access == access) &&
                db.Set<AssistantTurnWork>().Any(work => work.Id == row.Id && (work.Status == "Pending" || work.Status == "Running")))
                .Select(row => (Guid?)row.Id).SingleOrDefaultAsync(ct);

    public Task<IAssistantConversation?> GetConversationAsync(Guid owner, Access access, Guid id, uint? version, CancellationToken ct) => GetConversationCoreAsync(owner, access, id, version, ct);
    private async Task<IAssistantConversation?> GetConversationCoreAsync(Guid owner, Access access, Guid id, uint? version, CancellationToken ct)
    {
        IAssistantConversation? row = access == Access.Platform ? await platform.GetByIdAsync(owner, id, version, ct)
            : await conversations.GetByIdAsync(owner, access, id, version, ct);
        if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
    public async Task<IReadOnlyList<AssistantHistoryMessageRow>> FindHistoryPrivacyAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct)
    {
        if (access == Access.Platform)
            return await db.Set<PlatformAssistantMessage>().AsNoTracking().Where(row => row.ConversationId == conversationId &&
                db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.OwnerUserId == owner))
                .OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id)
                .Select(row => new AssistantHistoryMessageRow(row.Id, row.Role, row.PrivacySubjectsKnown, row.SubjectUserIds)).ToArrayAsync(ct);
        return await db.Set<AssistantMessage>().AsNoTracking().Where(row => row.ConversationId == conversationId && row.TenantId == tenant.RequiredTenantId &&
            db.Set<AssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.TenantId == row.TenantId && parent.OwnerUserId == owner && parent.Access == access))
            .OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id)
            .Select(row => new AssistantHistoryMessageRow(row.Id, row.Role, row.PrivacySubjectsKnown, row.SubjectUserIds)).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<AssistantMessageResponse>> ListMessagesAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct, IReadOnlyList<Guid>? messageIds = null)
    {
        if (access == Access.Platform)
        {
            var rows = await db.Set<PlatformAssistantMessage>().AsNoTracking().Where(row => row.ConversationId == conversationId && (messageIds == null || messageIds.Contains(row.Id)) &&
                db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.OwnerUserId == owner))
                .OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).ToArrayAsync(ct);
            var files = await db.Set<PlatformAssistantAttachment>().AsNoTracking().Where(row => row.ConversationId == conversationId && row.MessageId != null).ToArrayAsync(ct);
            return rows.Select(row => Message(row.Id, row.Role, row.Status, row.Text, row.CreatedAtUtc, row.Version) with
            { Attachments = files.Where(file => file.MessageId == row.Id).Select(file => new AssistantAttachmentSummaryResponse(file.Id, file.FileName, file.ContentType, file.Size, file.Version)).ToArray() }).ToArray();
        }
        else
        {
            var rows = await db.Set<AssistantMessage>().AsNoTracking().Where(row => row.ConversationId == conversationId && (messageIds == null || messageIds.Contains(row.Id)) && row.TenantId == tenant.RequiredTenantId &&
                db.Set<AssistantConversation>().Any(parent => parent.Id == row.ConversationId && parent.TenantId == row.TenantId && parent.OwnerUserId == owner && parent.Access == access))
                .OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).ToArrayAsync(ct);
            var files = await db.Set<AssistantAttachment>().AsNoTracking().Where(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId && row.MessageId != null).ToArrayAsync(ct);
            return rows.Select(row => Message(row.Id, row.Role, row.Status, row.Text, row.CreatedAtUtc, row.Version) with
            { Attachments = files.Where(file => file.MessageId == row.Id).Select(file => new AssistantAttachmentSummaryResponse(file.Id, file.FileName, file.ContentType, file.Size, file.Version)).ToArray() }).ToArray();
        }
    }
    private static AssistantMessageResponse Message(Guid id, string role, string status, string? text, DateTime createdAtUtc, uint version) =>
        new(id, Enum.Parse<AssistantMessageRole>(role), text ?? string.Empty, Enum.Parse<AssistantMessageStatus>(status), createdAtUtc, version, [], [], null);
    public async Task<bool> ValidateAttachmentsAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return true;
        if (await GetConversationAsync(owner, access, conversationId, null, ct) is null) return false;
        return access == Access.Platform
            ? await db.Set<PlatformAssistantAttachment>().CountAsync(row => row.ConversationId == conversationId && row.MessageId == null && ids.Contains(row.Id), ct) == ids.Count
            : await db.Set<AssistantAttachment>().CountAsync(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId && row.MessageId == null && ids.Contains(row.Id), ct) == ids.Count;
    }
    public IAssistantTurn Add(Guid conversationId, Access access, DateTime nowUtc)
    {
        db.RequireTransaction();
        IAssistantTurn row = access == Access.Platform ? PlatformAssistantTurn.Create(conversationId, nowUtc) : AssistantTurn.Create(conversationId, nowUtc);
        db.Add(row); return row;
    }
    public Guid AddMessage(Guid conversationId, Access access, string role, string status, string? text, DateTime nowUtc, IReadOnlyCollection<Guid>? subjects = null)
    {
        db.RequireTransaction();
        if (access == Access.Platform)
        {
            var row = PlatformAssistantMessage.Create(conversationId, role, status, text, nowUtc); row.SetPrivacySubjects(subjects ?? []); platform.Add(row); return row.Id;
        }
        else { var row = AssistantMessage.Create(conversationId, role, status, text, nowUtc); row.SetPrivacySubjects(subjects ?? []); conversations.Add(row); return row.Id; }
    }
    public void Add(AssistantTurnWork work) { db.RequireTransaction(); db.Add(work); }
    public void Touch(IAssistantConversation conversation, DateTime nowUtc)
    {
        conversation.Touch(nowUtc);
        if (conversation is PlatformAssistantConversation row) platform.MarkUpdated(row);
        else conversations.MarkUpdated((AssistantConversation)conversation);
    }
    public async Task CheckpointMessageAsync(Guid owner, Access access, IAssistantTurn turn, string? text, string? status, CancellationToken ct)
    {
        db.RequireTransaction();
        if (access == Access.Platform)
        {
            var row = await platform.GetMessageAsync(owner, turn.ConversationId, turn.AssistantMessageId, null, ct);
            if (row is null) return;
            if (db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
            if (text is not null) row.Checkpoint((row.Text ?? string.Empty) + text);
            if (status is not null) row.Finish(status);
        }
        else
        {
            var row = await conversations.GetMessageAsync(owner, access, turn.ConversationId, turn.AssistantMessageId, null, ct);
            if (row is null) return;
            if (db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
            if (text is not null) row.Checkpoint((row.Text ?? string.Empty) + text);
            if (status is not null) row.Finish(status);
        }
    }
    public async Task FinishMessageStatusAsync(Guid owner, Access access, IAssistantTurn turn, string status, CancellationToken ct)
    {
        db.RequireTransaction();
        // Materialize metadata only and save only Status. No text, links, tool arguments or results are read.
        // The existing parent join and RLS also make deletion a no-op, never an insertion.
        if (access == Access.Platform)
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
            if (row is null) return;
            row.Finish(status);
            db.Entry(row).Property(value => value.Status).IsModified = true;
        }
        else
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
            if (row is null) return;
            row.Finish(status);
            db.Entry(row).Property(value => value.Status).IsModified = true;
        }
    }
    public async Task AddSubjectsAsync(Guid owner, Access access, Guid bindingId, IReadOnlyList<Guid> subjects, CancellationToken ct)
    {
        db.RequireTransaction();
        await AssistantSnapshotRetentionWriter.LockIndicesAsync(db, [bindingId], ct);
        if (access == Access.Platform)
        {
            var row = await db.Set<PlatformAssistantDurableOperation>().SingleOrDefaultAsync(row => row.Id == bindingId && row.OwnerUserId == owner, ct);
            if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
            if (row?.SnapshotJson is null) return;
            row.AddSubjects(subjects);
            var turn = await db.Set<PlatformAssistantTurn>().SingleOrDefaultAsync(value => value.BindingId == bindingId && value.ConversationId == row.ConversationId, ct);
            if (turn is null) return;
            var message = await platform.GetMessageAsync(owner, row.ConversationId, turn.AssistantMessageId, null, ct);
            if (message is not null)
            {
                if (db.Entry(message).State == EntityState.Unchanged) await db.Entry(message).ReloadAsync(ct);
                message.AddPrivacySubjects(subjects);
            }
        }
        else
        {
            var row = await db.Set<AssistantDurableOperation>().SingleOrDefaultAsync(row => row.Id == bindingId && row.OwnerUserId == owner && row.Access == access && row.TenantId == tenant.RequiredTenantId, ct);
            if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
            if (row?.SnapshotJson is null) return;
            row.AddSubjects(subjects);
            var turn = await db.Set<AssistantTurn>().SingleOrDefaultAsync(value => value.BindingId == bindingId && value.ConversationId == row.ConversationId && value.TenantId == tenant.RequiredTenantId, ct);
            if (turn is null) return;
            var message = await conversations.GetMessageAsync(owner, access, row.ConversationId, turn.AssistantMessageId, null, ct);
            if (message is not null)
            {
                if (db.Entry(message).State == EntityState.Unchanged) await db.Entry(message).ReloadAsync(ct);
                message.AddPrivacySubjects(subjects);
            }
        }
    }
    public async Task SetSnapshotAsync(Guid owner, Access access, Guid bindingId, string? snapshot, CancellationToken ct)
    {
        db.RequireTransaction();
        await AssistantSnapshotRetentionWriter.LockIndicesAsync(db, [bindingId], ct);
        if (access == Access.Platform)
        {
            var row = await db.Set<PlatformAssistantDurableOperation>().SingleOrDefaultAsync(row => row.Id == bindingId && row.OwnerUserId == owner, ct);
            if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
            if (row?.SnapshotJson is not null) { if (snapshot is null) row.RemoveContent(); else row.SetTurnSnapshot(snapshot); }
        }
        else
        {
            var row = await db.Set<AssistantDurableOperation>().SingleOrDefaultAsync(row => row.Id == bindingId && row.OwnerUserId == owner && row.Access == access && row.TenantId == tenant.RequiredTenantId, ct);
            if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
            if (row?.SnapshotJson is not null) { if (snapshot is null) row.RemoveContent(); else row.SetTurnSnapshot(snapshot); }
        }
        if (snapshot is null) await AssistantSnapshotRetentionWriter.RemoveIndicesAsync(db, [bindingId], ct);
    }
    public async Task CancelConversationAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct)
    {
        db.RequireTransaction();
        Guid[] ids = access == Access.Platform
            ? await db.Set<PlatformAssistantTurn>().Where(row => row.ConversationId == conversationId && db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == conversationId && parent.OwnerUserId == owner)).Select(row => row.Id).ToArrayAsync(ct)
            : await db.Set<AssistantTurn>().Where(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId && db.Set<AssistantConversation>().Any(parent => parent.Id == conversationId && parent.OwnerUserId == owner && parent.Access == access && parent.TenantId == row.TenantId)).Select(row => row.Id).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            var work = await GetWorkAsync(id, true, ct);
            if (work is { IsTerminal: false }) work.RequestStop();
        }
    }
}
