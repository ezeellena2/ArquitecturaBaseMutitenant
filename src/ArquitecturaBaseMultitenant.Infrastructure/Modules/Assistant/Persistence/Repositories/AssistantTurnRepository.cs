using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantTurnRepository(ApplicationDbContext db, IEnumerable<AssistantTurnStore> stores,
    IAssistantAttachmentRepository attachments) : IAssistantTurnRepository
{
    private AssistantTurnStore Store(Access access) => stores.Single(store => store.Handles(access));
    private async Task<T?> ReloadAsync<T>(T? row, CancellationToken ct) where T : class
    {
        if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
    public Task BindAttachmentsAsync(Guid owner, Access access, Guid conversationId, Guid messageId, IReadOnlyList<Guid> ids, CancellationToken ct) => attachments.BindAsync(owner, access, conversationId, messageId, ids, ct);
    public async Task<IReadOnlyList<AssistantTurnDiscovery>> ListReadyAsync(DateTime nowUtc, CancellationToken ct) =>
        await db.Set<AssistantTurnWork>().AsNoTracking()
            .Where(row => row.Status == AssistantTurnStatus.Pending || row.Status == AssistantTurnStatus.Running && row.LeaseExpiresAtUtc <= nowUtc)
            // 0064 §6: lo que selló un motor más nuevo espera a la copia que lo conoce; vencido, esta lo cierra y devuelve el cupo.
            .Where(row => row.EngineVersion <= AssistantTurnWork.CurrentEngineVersion || row.DeadlineAtUtc <= nowUtc)
            .OrderBy(row => row.CreatedAtUtc).Take(AssistantTechnicalLimits.TurnsPerCopy)
            .Select(row => new AssistantTurnDiscovery(row.Id, new ExecutionActor(row.OwnerUserId, row.Access, row.ScopeTenantId, row.Culture, row.TimeZoneId)
            { TenantKind = row.TenantKind, SessionStartedAtUtc = row.SessionStartedAtUtc }, row.EngineVersion <= AssistantTurnWork.CurrentEngineVersion, row.TraceParent)).ToArrayAsync(ct);

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
        var row = await Store(access).GetAsync(owner, access, id, forUpdate, ct);
        if (forUpdate) return await ReloadAsync(row, ct);
        return row;
    }
    public Task<Guid?> FindActiveAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) => Store(access).FindActiveAsync(owner, access, conversationId, ct);
    public async Task<IAssistantConversation?> GetConversationAsync(Guid owner, Access access, Guid id, uint? version, CancellationToken ct)
    {
        var row = await ReloadAsync(await Store(access).GetConversationAsync(owner, access, id, version, ct), ct);
        // El contexto puede haber seguido la navegación antes de tomar el lock.
        // Recargar main no refresca su fila técnica de actividad.
        if (row is AssistantConversation local) await ReloadAsync(local.Activity, ct);
        else if (row is PlatformAssistantConversation global) await ReloadAsync(global.Activity, ct);
        return row;
    }
    public async Task<IReadOnlyList<AssistantHistoryMessageRow>> FindHistoryPrivacyAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) => await Store(access).FindHistoryPrivacyAsync(owner, access, conversationId, ct);
    public async Task<IReadOnlyList<AssistantMessageResponse>> ListMessagesAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct, IReadOnlyList<Guid>? messageIds = null)
    {
        var store = Store(access);
        var rows = await store.ListMessagesAsync(owner, access, conversationId, messageIds, ct);
        var files = await store.ListAttachmentsAsync(conversationId, ct);
        // ADR 0068 §8: el mensaje del SSE trae su propuesta, con su estado guardado.
        var ids = rows.Select(row => row.Id).ToArray();
        var proposals = access == Access.Platform ? []
            : await db.Set<AssistantProposal>().AsNoTracking().Where(row => row.ConversationId == conversationId && ids.Contains(row.MessageId))
                .Select(row => new { row.MessageId, row.Id, row.ActionId, row.ActionVersion, row.State }).ToArrayAsync(ct);
        return rows.Select(row => new AssistantMessageResponse(row.Id, Enum.Parse<AssistantMessageRole>(row.Role), row.Text ?? string.Empty,
            Enum.Parse<AssistantMessageStatus>(row.Status.ToString()), row.CreatedAtUtc, row.Version, [], [], null) with
        {
            Attachments = files.Where(file => file.MessageId == row.Id).Select(file => new AssistantAttachmentSummaryResponse(file.Id, file.FileName, file.ContentType, file.Size, file.Version)).ToArray(),
            Proposal = proposals.Where(proposal => proposal.MessageId == row.Id)
                .Select(proposal => AssistantMessageProposalResponse.From(proposal.Id, proposal.ActionId, proposal.ActionVersion, proposal.State)).SingleOrDefault(),
        }).ToArray();
    }
    public async Task<bool> ValidateAttachmentsAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return true;
        if (await GetConversationAsync(owner, access, conversationId, null, ct) is null) return false;
        return await Store(access).CountUnboundAttachmentsAsync(conversationId, ids, ct) == ids.Count;
    }
    public IAssistantTurn Add(Guid conversationId, Access access, DateTime nowUtc)
    {
        db.RequireTransaction();
        var row = Store(access).CreateTurn(conversationId, nowUtc);
        db.Add(row);
        return row;
    }
    public Guid AddMessage(Guid conversationId, Access access, string role, string status, string? text, DateTime nowUtc, IReadOnlyCollection<Guid>? subjects = null)
    {
        db.RequireTransaction();
        var row = Store(access).CreateMessage(conversationId, role, status, text, nowUtc);
        row.SetPrivacySubjects(subjects ?? []);
        db.Add(row);
        return row.Id;
    }
    public void Add(AssistantTurnWork work)
    {
        db.RequireTransaction();
        // 0064 §11: la traza de quien pide el turno (el request del Inicio), que continúa el worker; sin una, nada.
        if (AssistantTracing.Current() is { } trace) work.RecordTrace(trace);
        db.Add(work);
    }
    public void Touch(IAssistantConversation conversation, DateTime nowUtc)
    {
        conversation.Touch(nowUtc);
        db.Entry(conversation).State = EntityState.Modified;
    }
    public void TouchActivity(IAssistantConversation conversation, DateTime nowUtc) => conversation.Touch(nowUtc);
    public async Task CheckpointMessageAsync(Guid owner, Access access, IAssistantTurn turn, string? text, string? status, CancellationToken ct)
    {
        db.RequireTransaction();
        var row = await ReloadAsync(await Store(access).GetMessageAsync(owner, access, turn.ConversationId, turn.AssistantMessageId, ct), ct);
        if (row is null) return;
        if (text is not null) row.Checkpoint((row.Text ?? string.Empty) + text);
        if (status is not null) row.Finish(status);
    }
    public async Task FinishMessageStatusAsync(Guid owner, Access access, IAssistantTurn turn, string status, CancellationToken ct)
    {
        db.RequireTransaction();
        var row = await Store(access).GetMessageMetadataAsync(owner, access, turn, ct);
        if (row is null) return;
        row.Finish(status);
        db.Entry(row).Property(value => value.Status).IsModified = true;
    }
    public async Task AddSubjectsAsync(Guid owner, Access access, Guid bindingId, IReadOnlyList<Guid> subjects, CancellationToken ct)
    {
        db.RequireTransaction();
        await AssistantSnapshotRetentionWriter.LockIndicesAsync(db, [bindingId], ct);
        var store = Store(access);
        var row = await ReloadAsync(await store.GetBindingAsync(owner, access, bindingId, ct), ct);
        if (row?.SnapshotJson is null) return;
        row.AddSubjects(subjects);
        var turn = await store.GetBindingTurnAsync(bindingId, row.ConversationId, ct);
        if (turn is null) return;
        var message = await ReloadAsync(await store.GetMessageAsync(owner, access, row.ConversationId, turn.AssistantMessageId, ct), ct);
        message?.AddPrivacySubjects(subjects);
    }
    public async Task SetSnapshotAsync(Guid owner, Access access, Guid bindingId, string? snapshot, CancellationToken ct)
    {
        db.RequireTransaction();
        await AssistantSnapshotRetentionWriter.LockIndicesAsync(db, [bindingId], ct);
        var row = await ReloadAsync(await Store(access).GetBindingAsync(owner, access, bindingId, ct), ct);
        if (row?.SnapshotJson is not null) { if (snapshot is null) row.RemoveContent(); else row.SetTurnSnapshot(snapshot); }
        if (snapshot is null) await AssistantSnapshotRetentionWriter.RemoveIndicesAsync(db, [bindingId], ct);
    }
    public async Task CancelConversationAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct)
    {
        db.RequireTransaction();
        foreach (var id in await Store(access).ListTurnIdsAsync(owner, access, conversationId, ct))
        {
            var work = await GetWorkAsync(id, true, ct);
            if (work is { IsTerminal: false }) work.RequestStop();
        }
    }
}
