using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed record AssistantQuickPreparation(Guid OwnerUserId, Access Access, Guid? TenantId,
    IdempotentRequest Request, string Key, AssistantQuickRequest Input, AssistantDurableOperationRow? Existing,
    Result<AssistantQuickCompositionResponse>? Composition)
{
    internal IReadOnlyCollection<Guid> Subjects => Existing?.SubjectUserIds
        ?? (Composition is { IsSuccess: true } result
            ? result.Value.SubjectCopies.Select(copy => copy.AccountId).Distinct().ToArray() : []);
}

/// <summary>Captures one composition outside the write boundary, retaining failures until the binding is rechecked.</summary>
internal sealed class AssistantQuickPreparationBuilder(IAssistantConversationReader conversations,
    IAssistantDurableRepository durable, ICurrentUser user, ITenantContext tenant,
    IIdempotentRequestContext context, AssistantConversationPolicy policy, IAssistantQuickService quick)
{
    internal async Task<Result<AssistantQuickPreparation>> PrepareAsync(string key, AssistantQuickRequest input, CancellationToken ct)
    {
        var request = context.Request ?? throw new InvalidOperationException("A durable request context is required.");
        var owner = user.UserId ?? throw new InvalidOperationException("A session is required.");
        var access = user.Access ?? throw new InvalidOperationException("An access is required.");
        var allowed = await policy.AuthorizeQuickAsync(key, [], ct);
        if (allowed.IsFailure) return allowed.Error;
        var existing = await durable.GetByKeyAsync(owner, access, request.Key, ct);
        if (existing is not null)
            return new AssistantQuickPreparation(owner, access, tenant.TenantId, request, key, input, existing, null);
        if (input.ConversationId is { } id)
        {
            var conversation = await conversations.FindByIdAsync(owner, access, id, ct);
            if (conversation is null) return AssistantErrors.ConversationNotFound;
            if (input.Version is { } version && version != conversation.Version)
                throw new ConcurrencyConflictException("The row changed since it was read.");
        }
        var composition = await quick.ExecuteAsync(key, ct);
        return new AssistantQuickPreparation(owner, access, tenant.TenantId, request, key, input, null, composition);
    }
}
