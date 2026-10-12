using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantAttachmentRepository(ApplicationDbContext db, ITenantContext tenant) : IAssistantAttachmentRepository
{
    public async Task<AssistantAttachmentRow?> GetAsync(Guid owner, Access access, Guid conversationId, Guid attachmentId, CancellationToken ct) =>
        (await ListAsync(owner, access, conversationId, [attachmentId], ct)).SingleOrDefault();

    public async Task<IReadOnlyList<AssistantAttachmentRow>> ListAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (access == Access.Platform)
            return await db.Set<PlatformAssistantAttachment>().AsNoTracking().Where(row => row.ConversationId == conversationId && ids.Contains(row.Id) &&
                db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == conversationId && parent.OwnerUserId == owner))
                .Select(row => new AssistantAttachmentRow(row.Id, row.ConversationId, row.MessageId, row.FileName, row.ContentType, row.StorageKey, row.Size, row.Version)).ToArrayAsync(ct);
        return await db.Set<AssistantAttachment>().AsNoTracking().Where(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId && ids.Contains(row.Id) &&
            db.Set<AssistantConversation>().Any(parent => parent.Id == conversationId && parent.TenantId == row.TenantId && parent.OwnerUserId == owner && parent.Access == access))
            .Select(row => new AssistantAttachmentRow(row.Id, row.ConversationId, row.MessageId, row.FileName, row.ContentType, row.StorageKey, row.Size, row.Version)).ToArrayAsync(ct);
    }

    public IVersioned Add(Access access, Guid conversationId, string fileName, string mediaType, string storageKey, long length, DateTime nowUtc)
    {
        db.RequireTransaction();
        Entity row = access == Access.Platform ? PlatformAssistantAttachment.Create(conversationId, fileName, mediaType, storageKey, length, nowUtc)
            : AssistantAttachment.Create(conversationId, fileName, mediaType, storageKey, length, nowUtc);
        db.Add(row);
        return (IVersioned)row;
    }

    public async Task RemoveAsync(Guid owner, Access access, AssistantAttachmentRow attachment, uint expectedVersion, DateTime nowUtc, CancellationToken ct)
    {
        db.RequireTransaction();
        if (attachment.Version != expectedVersion) throw new ConcurrencyConflictException("The attachment changed.");
        if (access == Access.Platform)
        {
            var row = await db.Set<PlatformAssistantAttachment>().SingleAsync(value => value.Id == attachment.Id && value.ConversationId == attachment.ConversationId, ct);
            db.Entry(row).Property(value => value.Version).OriginalValue = expectedVersion;
            db.Add(PlatformAssistantFileCleanup.Create(row.Id, owner, row.StorageKey, nowUtc));
            db.Remove(row);
        }
        else
        {
            var row = await db.Set<AssistantAttachment>().SingleAsync(value => value.Id == attachment.Id && value.ConversationId == attachment.ConversationId && value.TenantId == tenant.RequiredTenantId, ct);
            db.Entry(row).Property(value => value.Version).OriginalValue = expectedVersion;
            db.Add(AssistantFileCleanup.Create(row.Id, owner, row.StorageKey, nowUtc));
            db.Remove(row);
        }
    }

    public async Task BindAsync(Guid owner, Access access, Guid conversationId, Guid messageId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        db.RequireTransaction();
        if ((await ListAsync(owner, access, conversationId, ids, ct)).Count != ids.Count) throw new InvalidOperationException("Only authorized attachments can be bound.");
        if (access == Access.Platform)
        {
            foreach (var row in await db.Set<PlatformAssistantAttachment>().Where(row => row.ConversationId == conversationId && ids.Contains(row.Id)).ToArrayAsync(ct)) row.Bind(messageId);
        }
        else
        {
            foreach (var row in await db.Set<AssistantAttachment>().Where(row => row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId && ids.Contains(row.Id)).ToArrayAsync(ct)) row.Bind(messageId);
        }
    }
}
