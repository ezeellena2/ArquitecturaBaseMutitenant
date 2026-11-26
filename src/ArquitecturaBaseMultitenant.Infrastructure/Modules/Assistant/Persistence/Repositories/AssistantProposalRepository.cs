using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

/// <summary>Las propuestas de las acciones en la organización del claim: RLS, la organización y el dueño de la conversación.</summary>
internal sealed class AssistantProposalRepository(ApplicationDbContext db, ITenantContext tenant) : IAssistantProposalRepository
{
    public Task<AssistantProposal?> FindAsync(Guid owner, Access access, Guid id, CancellationToken ct) =>
        Owned(owner, access).AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct);

    public async Task<AssistantProposal?> LockAsync(Guid owner, Access access, Guid id, CancellationToken ct)
    {
        db.RequireTransaction();
        if (access == Access.Platform) return null;
        var tenantId = tenant.RequiredTenantId;
        var locked = await db.Set<AssistantProposal>().FromSqlInterpolated($"""
            SELECT *, xmin FROM tenant."AssistantProposals"
            WHERE "TenantId" = {tenantId} AND "Id" = {id} AND "OwnerUserId" = {owner}
            FOR UPDATE
            """).ToArrayAsync(ct);
        // Bloqueada la fila, se confirma que la conversación sea de su dueño y de su acceso; la instancia es la misma, seguida.
        return locked.Length == 0 ? null : await Owned(owner, access).SingleOrDefaultAsync(row => row.Id == id, ct);
    }

    public async Task<IReadOnlyList<Guid>> ListSubjectsAsync(AssistantProposal proposal, CancellationToken ct) =>
        await db.Set<AssistantMessage>().AsNoTracking()
            .Where(row => row.TenantId == proposal.TenantId && row.ConversationId == proposal.ConversationId && row.Id == proposal.MessageId)
            .Select(row => row.SubjectUserIds).SingleOrDefaultAsync(ct) ?? [];

    public async Task<IReadOnlyList<AssistantProposal>> ListByMessagesAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> messageIds,
        CancellationToken ct) => messageIds.Count == 0 ? []
        : await Owned(owner, access).AsNoTracking().Where(row => row.ConversationId == conversationId && messageIds.Contains(row.MessageId)).ToArrayAsync(ct);

    public Task<bool> ExistsForMessageAsync(Guid conversationId, Guid messageId, CancellationToken ct) =>
        db.Set<AssistantProposal>().AsNoTracking().AnyAsync(row => row.TenantId == tenant.RequiredTenantId
            && row.ConversationId == conversationId && row.MessageId == messageId, ct);

    public async Task<IReadOnlyList<AssistantProposal>> ListNewAsync(Guid conversationId, string kind, CancellationToken ct)
    {
        db.RequireTransaction();
        return await db.Set<AssistantProposal>().Where(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId
            && row.Kind == kind && row.State == AssistantProposalStatus.New).ToArrayAsync(ct);
    }

    public void Add(AssistantProposal proposal)
    {
        db.RequireTransaction();
        db.Add(proposal);
    }

    // Solo el dueño, en una conversación suya y de su acceso; Plataforma no tiene propuestas de organización.
    private IQueryable<AssistantProposal> Owned(Guid owner, Access access)
    {
        if (access == Access.Platform) return db.Set<AssistantProposal>().Where(_ => false);
        var tenantId = tenant.RequiredTenantId;
        return db.Set<AssistantProposal>().Where(row => row.TenantId == tenantId && row.OwnerUserId == owner
            && db.Set<AssistantConversation>().Any(parent => parent.TenantId == row.TenantId && parent.Id == row.ConversationId
                && parent.OwnerUserId == owner && parent.Access == access));
    }
}
