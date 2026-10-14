using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Las bandejas Clientes e Internos de la organización del claim (ADR 0038). Un chat se toma bajo el lock de su fila: si
/// otra persona lo tomó antes, el segundo recibe TakenByOther y su mensaje no sale. Responder un chat sin tomar lo toma.
/// Las notas nunca salen a Meta. Pasar a un equipo lo deja sin tomar en ese equipo; pasar a una persona, tomado por ella.
/// </summary>
internal sealed class WhatsAppConversationService(IWhatsAppChatRepository chats, IWhatsAppTeamRepository teams, IWhatsAppTeamReader teamReader,
    WhatsAppTeamResponseBuilder teamPeople, WhatsAppConversationResponseBuilder responses, WhatsAppReplyIssuer replies, ICurrentUser user, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock,
    ILogger<WhatsAppConversationService> logger) : IWhatsAppConversationService
{
    public Task<Result<WhatsAppConversationListResponse>> ListAsync(WhatsAppInbox? inbox, WhatsAppConversationListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppConversationListResponse>(logger, clock, "WhatsAppConversationList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await responses.ListAsync(Box(inbox), request, cancellationToken);
        });

    public Task<Result<WhatsAppConversationResponse>> GetAsync(WhatsAppInbox? inbox, Guid id, WhatsAppConversationMessagesRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppConversationResponse>(logger, clock, "WhatsAppConversationGet", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await responses.ConversationAsync(Box(inbox), id, request, cancellationToken) is { } conversation
                ? conversation : WhatsAppErrors.ConversationNotFound;
        });

    public Task<Result<WhatsAppConversationRowResponse>> TakeAsync(WhatsAppInbox? inbox, Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppConversationRowResponse>(logger, clock, "WhatsAppConversationTake", async () =>
        {
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await chats.GetForUpdateAsync(id, Box(inbox), null, ct) is not { } chat) return Result.Failure(WhatsAppErrors.ConversationNotFound);
                return await TakeAsync(chat, ct);
            }, CommitPolicy.OnSuccess, cancellationToken);
            return saved.IsFailure ? saved.Error : await RowAsync(inbox, id, cancellationToken);
        });

    public Task<Result<WhatsAppChatMessageResponse>> SendAsync(WhatsAppInbox? inbox, Guid id, SendWhatsAppMessageRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppChatMessageResponse>(logger, clock, "WhatsAppConversationSend", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var text = request.Text!.Trim();
            return await unitOfWork.ExecuteInTransactionAsync<Result<WhatsAppChatMessageResponse>>(async ct =>
            {
                if (await chats.GetForUpdateAsync(id, Box(inbox), null, ct) is not { } chat) return WhatsAppErrors.ConversationNotFound;
                var taken = await TakeAsync(chat, ct);
                if (taken.IsFailure) return taken.Error;
                var issued = await replies.IssueAsync(chat, text, ct);
                if (issued.IsFailure) return issued.Error;
                var nowUtc = clock.GetUtcNow().UtcDateTime;
                var message = WhatsAppChatMessage.Outbound(chat.Id, WhatsAppChatMessageKind.Text, Me, await MyNameAsync(ct), text, issued.Value, null, nowUtc);
                chats.Add(message);
                chat.RecordOutbound(nowUtc, text);
                return Response(message);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<WhatsAppChatMessageResponse>> AddNoteAsync(WhatsAppInbox? inbox, Guid id, AddWhatsAppNoteRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppChatMessageResponse>(logger, clock, "WhatsAppConversationAddNote", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync<Result<WhatsAppChatMessageResponse>>(async ct =>
            {
                if (await chats.GetForUpdateAsync(id, Box(inbox), null, ct) is not { } chat) return WhatsAppErrors.ConversationNotFound;
                var note = WhatsAppChatMessage.Note(chat.Id, Me, await MyNameAsync(ct), request.Text!.Trim(), clock.GetUtcNow().UtcDateTime);
                chats.Add(note);
                return Response(note);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<WhatsAppConversationRowResponse>> TransferAsync(WhatsAppInbox? inbox, Guid id, TransferWhatsAppConversationRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppConversationRowResponse>(logger, clock, "WhatsAppConversationTransfer", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            Guid? userId = null;
            if (request.MemberId is { } memberId)
            {
                if ((await teamReader.ListActiveMembersAsync([memberId], cancellationToken)).SingleOrDefault() is not { } member)
                    return WhatsAppTeamService.Invalid("memberId", "WhatsApp.Validation.SelectionInvalid");
                userId = member.UserId;
            }
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await chats.GetForUpdateAsync(id, Box(inbox), request.Version, ct) is not { } chat)
                    return Result.Failure(WhatsAppErrors.ConversationNotFound);
                // Un equipo de la otra bandeja no existe para esta conversación.
                if (request.TeamId is { } teamId && (await teams.ListAsync([teamId], ct)).SingleOrDefault()?.Inbox != chat.Inbox)
                    return Result.Failure(WhatsAppErrors.TeamNotFound);
                chat.Transfer(request.TeamId, userId, clock.GetUtcNow().UtcDateTime);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            return saved.IsFailure ? saved.Error : await RowAsync(inbox, id, cancellationToken);
        });

    public Task<Result<PagedResult<WhatsAppTemplateOptionResponse>>> ListTemplateOptionsAsync(WhatsAppInbox? inbox, Guid id,
        WhatsAppTemplateOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationListTemplateOptions",
            () => Task.FromResult(Result.Failure<PagedResult<WhatsAppTemplateOptionResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppChatMessageResponse>> SendTemplateAsync(WhatsAppInbox? inbox, Guid id, SendWhatsAppTemplateRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationSendTemplate",
            () => Task.FromResult(Result.Failure<WhatsAppChatMessageResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PagedResult<WhatsAppTransferTeamResponse>>> ListTransferTeamsAsync(WhatsAppInbox? inbox, Guid id,
        WhatsAppTransferOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppTransferTeamResponse>>(logger, clock, "WhatsAppConversationListTransferTeams", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await responses.RowAsync(Box(inbox), id, cancellationToken) is null) return WhatsAppErrors.ConversationNotFound;
            var page = await teamReader.ListOrganizationAsync(Box(inbox), request, cancellationToken);
            var people = await teamPeople.PeopleAsync(page.Items.SelectMany(team => team.MemberIds), platform: false, cancellationToken);
            return new PagedResult<WhatsAppTransferTeamResponse>([.. page.Items.Select(team =>
                new WhatsAppTransferTeamResponse(team.Id, team.Name, team.MemberIds.Count(people.ContainsKey)))], page.Page, page.PageSize, page.TotalCount);
        });

    public Task<Result<PagedResult<WhatsAppTransferPersonResponse>>> ListTransferPeopleAsync(WhatsAppInbox? inbox, Guid id,
        WhatsAppTransferOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppTransferPersonResponse>>(logger, clock, "WhatsAppConversationListTransferPeople", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await responses.RowAsync(Box(inbox), id, cancellationToken) is not { } row) return WhatsAppErrors.ConversationNotFound;
            // Las personas del equipo del chat; sin equipo, las de todos los equipos de la bandeja.
            var teamsOfChat = row.Team is { } team
                ? [.. await teamReader.FindOrganizationAsync(team.Id, cancellationToken) is { } found ? [found] : Array.Empty<WhatsAppTeamRow>()]
                : (await teamReader.ListOrganizationAsync(Box(inbox), new WhatsAppTeamListRequest { PageSize = 100 }, cancellationToken)).Items;
            var people = await teamPeople.PeopleAsync(teamsOfChat.SelectMany(value => value.MemberIds), platform: false, cancellationToken);
            return await responses.PeopleAsync(Box(inbox), people, request, cancellationToken);
        });

    public Task<Result<WhatsAppUntakenResponse>> GetUntakenAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppUntakenResponse>(logger, clock, "WhatsAppConversationGetUntaken", async () =>
            await responses.UntakenAsync(cancellationToken));

    public Task<Result<PagedResult<WhatsAppReferenceResponse>>> ListInboxTeamsAsync(WhatsAppInbox? inbox, WhatsAppInboxTeamListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppReferenceResponse>>(logger, clock, "WhatsAppConversationInboxTeams", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await teamReader.ListOrganizationAsync(Box(inbox), request, cancellationToken);
            return new PagedResult<WhatsAppReferenceResponse>([.. page.Items.Select(team => new WhatsAppReferenceResponse(team.Id, team.Name))],
                page.Page, page.PageSize, page.TotalCount);
        });

    private Guid Me => user.UserId ?? throw new InvalidOperationException("An authenticated user is required.");

    private static WhatsAppInbox Box(WhatsAppInbox? inbox) => inbox ?? throw new InvalidOperationException("The organization inbox is required.");

    /// <summary>Lo toma quien escribe; si ya es de otra persona, TakenByOther con su nombre y nada sale.</summary>
    private async Task<Result> TakeAsync(WhatsAppChat chat, CancellationToken ct)
    {
        if (chat.Take(Me, clock.GetUtcNow().UtcDateTime).IsSuccess) return Result.Success();
        var taker = chat.AssigneeUserId!.Value;
        return Result.Failure(WhatsAppErrors.ConversationTakenByOther(await responses.NameAsync(taker, ct) ?? string.Empty));
    }

    private Task<string?> MyNameAsync(CancellationToken ct) => responses.NameAsync(Me, ct);

    private async Task<Result<WhatsAppConversationRowResponse>> RowAsync(WhatsAppInbox? inbox, Guid id, CancellationToken ct) =>
        await responses.RowAsync(Box(inbox), id, ct) is { } row ? row : WhatsAppErrors.ConversationNotFound;

    internal static WhatsAppChatMessageResponse Response(WhatsAppChatMessageBase message) => new(message.Id, message.Kind, message.Direction,
        message.AuthorName, message.Text, message.MediaKind, message.Status, message.FailureReason, message.CreatedAtUtc);
}
