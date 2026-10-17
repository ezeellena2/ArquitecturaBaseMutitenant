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
    public Task<AssistantConversationSummaryResponse?> FindByIdAsync(Guid ownerUserId, Access access, Guid conversationId, CancellationToken ct)
    {
        Validate(ownerUserId, access);
        if (access == Access.Platform)
            return context.Set<PlatformAssistantConversation>().AsNoTracking().Where(value => value.Id == conversationId && value.OwnerUserId == ownerUserId)
                .Select(value => new AssistantConversationSummaryResponse(value.Id, value.Title, value.IsSaved, value.Version, value.CreatedAtUtc, value.LastActivityAtUtc)).SingleOrDefaultAsync(ct);
        var tenantId = tenant.RequiredTenantId;
        return context.Set<AssistantConversation>().AsNoTracking().Where(value => value.Id == conversationId && value.TenantId == tenantId && value.OwnerUserId == ownerUserId && value.Access == access)
            .Select(value => new AssistantConversationSummaryResponse(value.Id, value.Title, value.IsSaved, value.Version, value.CreatedAtUtc, value.LastActivityAtUtc)).SingleOrDefaultAsync(ct);
    }

    public async Task<CursorResult<AssistantConversationSummaryResponse>> ListAsync(Guid ownerUserId, Access access, AssistantConversationListRequest request, CancellationToken ct)
    {
        Validate(ownerUserId, access);
        var tenantId = access == Access.Platform ? Guid.Empty : tenant.RequiredTenantId;
        var query = access == Access.Platform
            ? context.Set<PlatformAssistantConversation>().AsNoTracking().Where(value => value.OwnerUserId == ownerUserId && (request.Saved == null || value.IsSaved == request.Saved))
                .Select(value => new { value.Id, value.Title, value.IsSaved, value.Version, value.CreatedAtUtc, value.LastActivityAtUtc })
            : context.Set<AssistantConversation>().AsNoTracking().Where(value => value.TenantId == tenantId && value.OwnerUserId == ownerUserId && value.Access == access && (request.Saved == null || value.IsSaved == request.Saved))
                .Select(value => new { value.Id, value.Title, value.IsSaved, value.Version, value.CreatedAtUtc, value.LastActivityAtUtc });
        var page = await AssistantCursorPagination.ToCursorResultAsync(query, request.Limit ?? AssistantTechnicalLimits.QuickRows, request.Cursor,
            $"conversations:{tenantId:N}:{ownerUserId:N}:{access}:{request.Saved?.ToString() ?? "all"}", false,
            value => value.LastActivityAtUtc, value => value.Id, ct);
        return new CursorResult<AssistantConversationSummaryResponse>([.. page.Items.Select(value =>
            new AssistantConversationSummaryResponse(value.Id, value.Title, value.IsSaved, value.Version, value.CreatedAtUtc, value.LastActivityAtUtc))], page.NextCursor, page.HasMore);
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
        return new CursorResult<AssistantMessageResponse>([.. page.Items.Select(value => new AssistantMessageResponse(value.Id,
            Enum.Parse<AssistantMessageRole>(value.Role), value.Text ?? "", Enum.Parse<AssistantMessageStatus>(value.Status.ToString()), value.CreatedAtUtc, value.Version,
            attachments.GetValueOrDefault(value.Id) ?? [], value.LinksJson is null ? [] : JsonSerializer.Deserialize<AssistantLinkResponse[]>(value.LinksJson)!,
            value.PositiveFeedback == null ? null : value.PositiveFeedback.Value ? AssistantRating.Up : AssistantRating.Down))], page.NextCursor, page.HasMore);
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
