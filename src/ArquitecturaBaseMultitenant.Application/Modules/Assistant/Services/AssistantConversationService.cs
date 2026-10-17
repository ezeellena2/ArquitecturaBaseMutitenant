using UniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence.UniqueKeys;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantConversationService(IAssistantConversationReader reader, ICurrentUser user, AssistantNoticeWriter notice,
    AssistantConversationWriter writer, AssistantConversationPolicy policy, AssistantQuickPreparationBuilder preparation,
    AssistantStateBuilder state, AssistantTurnRequester turns, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock, ILogger<AssistantConversationService> logger) : IAssistantConversationService
{
    public Task<Result<AssistantStateResponse>> StateAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationState", () => state.BuildAsync(cancellationToken));

    public Task<Result> AcceptNoticeAsync(AssistantNoticeRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationAcceptNotice", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(ct => notice.AcceptAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<CursorResult<AssistantConversationSummaryResponse>>> ListAsync(AssistantConversationListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<CursorResult<AssistantConversationSummaryResponse>>(logger, clock, "AssistantConversationList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var allowed = await policy.AuthorizeAsync(cancellationToken);
            if (allowed.IsFailure) return allowed.Error;
            return await reader.ListAsync(user.UserId ?? throw new InvalidOperationException("A session is required."), user.Access ?? throw new InvalidOperationException("An access is required."), request, cancellationToken);
        });

    public Task<Result<IdempotentReplayResponse<AssistantConversationSummaryResponse>>> CreateAsync(CreateAssistantConversationRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IdempotentReplayResponse<AssistantConversationSummaryResponse>>(logger, clock, "AssistantConversationCreate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(writer.CreateAsync, CommitPolicy.OnSuccess, cancellationToken);
            return saved.IsFailure ? saved.Error : new IdempotentReplayResponse<AssistantConversationSummaryResponse>(saved.Value.Reference, saved.Value.AfterSave().Conversation);
        });

    public Task<Result<AssistantConversationResponse>> GetAsync(Guid id, AssistantMessageListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantConversationResponse>(logger, clock, "AssistantConversationGet", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var allowed = await policy.AuthorizeAsync(cancellationToken);
            if (allowed.IsFailure) return allowed.Error;
            var ownerId = user.UserId ?? throw new InvalidOperationException("A session is required.");
            var access = user.Access ?? throw new InvalidOperationException("An access is required.");
            var conversation = await reader.FindByIdAsync(ownerId, access, id, cancellationToken);
            if (conversation is null) return AssistantErrors.ConversationNotFound;
            var messages = await reader.ListMessagesAsync(ownerId, access, id, request, cancellationToken);
            var filtered = await policy.FilterHistoryLinksAsync(messages.Items, cancellationToken);
            if (filtered.IsFailure) return filtered.Error;
            allowed = await policy.AuthorizeAsync(cancellationToken);
            if (allowed.IsFailure) return allowed.Error;
            return new AssistantConversationResponse(conversation, messages with { Items = filtered.Value }, await turns.ActiveAsync(ownerId, access, id, cancellationToken));
        });

    public Task<Result<VersionResponse>> UpdateAsync(Guid id, UpdateAssistantConversationRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "AssistantConversationUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(ct => writer.UpdateAsync(id, request, ct), CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(saved);
        });

    public Task<Result> DeleteAsync(Guid id, uint? version, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationDelete", async () =>
        {
            if (await validator.ValidateAsync(new AssistantDeleteRequest(version), cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(ct => writer.DeleteAsync(id, version, ct), CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<AssistantTurnAdmission>> SendAsync(Guid id, SendAssistantMessageRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantTurnAdmission>(logger, clock, "AssistantConversationSend", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (request.AttachmentIds!.Count > AssistantTechnicalLimits.MaxAttachments) return AssistantErrors.AttachmentTooMany;
            var prepared = await turns.PrepareAsync(id, cancellationToken);
            if (prepared.IsFailure) return prepared.Error;
            try { return await unitOfWork.ExecuteInTransactionAsync(ct => turns.AdmitAsync(id, request, prepared.Value, ct), CommitPolicy.OnSuccess, cancellationToken); }
            catch (ArquitecturaBaseMultitenant.Application.Common.Exceptions.UniqueConstraintViolationException exception)
                when (exception.ConstraintName is UniqueKeys.AssistantActiveTurn or UniqueKeys.PlatformAssistantActiveTurn)
            { return AssistantErrors.TurnInProgress; }
        });

    public Task<Result> StopAsync(Guid id, Guid turnId, StopAssistantTurnRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationStop", async () =>
        {
            var prepared = await turns.PrepareAsync(null, cancellationToken);
            if (prepared.IsFailure) return prepared.Error;
            return await unitOfWork.ExecuteInTransactionAsync(ct => turns.StopAsync(id, turnId, prepared.Value, ct), CommitPolicy.OnSuccess, cancellationToken);
        });
    public Task<Result<IdempotentReplayResponse<AssistantQuickResponse>>> QuickAsync(string key, AssistantQuickRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IdempotentReplayResponse<AssistantQuickResponse>>(logger, clock, "AssistantConversationQuick", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var prepared = await preparation.PrepareAsync(key, request, cancellationToken);
            if (prepared.IsFailure) return prepared.Error;
            var saved = await unitOfWork.ExecuteInTransactionAsync(ct => writer.QuickAsync(prepared.Value, ct), CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            var snapshot = saved.Value.AfterSave();
            return new IdempotentReplayResponse<AssistantQuickResponse>(saved.Value.Reference,
                new AssistantQuickResponse(snapshot.Conversation, snapshot.Messages, snapshot.Result!));
        });

    public Task<Result<VersionResponse>> FeedbackAsync(Guid id, Guid messageId, AssistantFeedbackRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "AssistantConversationFeedback", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(ct => writer.FeedbackAsync(id, messageId, request, ct), CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(saved);
        });

}
