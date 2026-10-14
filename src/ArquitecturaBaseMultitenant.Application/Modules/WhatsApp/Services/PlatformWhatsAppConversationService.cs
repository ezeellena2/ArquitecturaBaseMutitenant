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
/// Las consultas al número de Plataforma (ADR 0038, precisión del 03/10), con las mismas reglas que las bandejas de la
/// organización: tomar bajo lock, responder dentro de las 24 horas, notas que no salen y pasar a un equipo de operadores o a
/// un operador. La cuenta del número va solo a quien puede ver cuentas.
/// </summary>
internal sealed class PlatformWhatsAppConversationService(IPlatformWhatsAppChatRepository chats, IPlatformWhatsAppTeamRepository teams,
    IWhatsAppTeamReader teamReader, WhatsAppTeamResponseBuilder teamPeople, PlatformWhatsAppOperatorResolver operators,
    WhatsAppConversationResponseBuilder responses, WhatsAppReplyIssuer replies, ICurrentUser user, IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<PlatformWhatsAppConversationService> logger)
    : IPlatformWhatsAppConversationService
{
    public Task<Result<WhatsAppConversationListResponse>> ListAsync(WhatsAppInbox? inbox, WhatsAppConversationListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppConversationListResponse>(logger, clock, "PlatformWhatsAppConversationList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await responses.ListAsync(null, request, cancellationToken);
        });

    public Task<Result<WhatsAppConversationResponse>> GetAsync(WhatsAppInbox? inbox, Guid id, WhatsAppConversationMessagesRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppConversationResponse>(logger, clock, "PlatformWhatsAppConversationGet", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await responses.ConversationAsync(null, id, request, cancellationToken) is { } conversation
                ? conversation : WhatsAppErrors.ConversationNotFound;
        });

    public Task<Result<WhatsAppConversationRowResponse>> TakeAsync(WhatsAppInbox? inbox, Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppConversationRowResponse>(logger, clock, "PlatformWhatsAppConversationTake", async () =>
        {
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
                await chats.GetForUpdateAsync(id, null, ct) is { } chat ? await TakeAsync(chat, ct) : Result.Failure(WhatsAppErrors.ConversationNotFound),
                CommitPolicy.OnSuccess, cancellationToken);
            return saved.IsFailure ? saved.Error : await RowAsync(id, cancellationToken);
        });

    public Task<Result<WhatsAppChatMessageResponse>> SendAsync(WhatsAppInbox? inbox, Guid id, SendWhatsAppMessageRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppChatMessageResponse>(logger, clock, "PlatformWhatsAppConversationSend", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var text = request.Text!.Trim();
            return await unitOfWork.ExecuteInTransactionAsync<Result<WhatsAppChatMessageResponse>>(async ct =>
            {
                if (await chats.GetForUpdateAsync(id, null, ct) is not { } chat) return WhatsAppErrors.ConversationNotFound;
                var taken = await TakeAsync(chat, ct);
                if (taken.IsFailure) return taken.Error;
                var issued = await replies.IssueAsync(chat, text, ct);
                if (issued.IsFailure) return issued.Error;
                var nowUtc = clock.GetUtcNow().UtcDateTime;
                var message = PlatformWhatsAppChatMessage.Outbound(chat.Id, WhatsAppChatMessageKind.Text, Me, await MyNameAsync(ct), text,
                    issued.Value, null, nowUtc);
                chats.Add(message);
                chat.RecordOutbound(nowUtc, text);
                return WhatsAppConversationService.Response(message);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<WhatsAppChatMessageResponse>> AddNoteAsync(WhatsAppInbox? inbox, Guid id, AddWhatsAppNoteRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppChatMessageResponse>(logger, clock, "PlatformWhatsAppConversationAddNote", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync<Result<WhatsAppChatMessageResponse>>(async ct =>
            {
                if (await chats.GetForUpdateAsync(id, null, ct) is not { } chat) return WhatsAppErrors.ConversationNotFound;
                var note = PlatformWhatsAppChatMessage.Note(chat.Id, Me, await MyNameAsync(ct), request.Text!.Trim(), clock.GetUtcNow().UtcDateTime);
                chats.Add(note);
                return WhatsAppConversationService.Response(note);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<WhatsAppConversationRowResponse>> TransferAsync(WhatsAppInbox? inbox, Guid id, TransferWhatsAppConversationRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppConversationRowResponse>(logger, clock, "PlatformWhatsAppConversationTransfer", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            // En Plataforma la persona es una cuenta de operador.
            if (request.MemberId is { } accountId && !await operators.AreOperatorsAsync([accountId], cancellationToken))
                return WhatsAppTeamService.Invalid("memberId", "WhatsApp.Validation.SelectionInvalid");
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await chats.GetForUpdateAsync(id, request.Version, ct) is not { } chat) return Result.Failure(WhatsAppErrors.ConversationNotFound);
                if (request.TeamId is { } teamId && (await teams.ListAsync([teamId], ct)).Count == 0) return Result.Failure(WhatsAppErrors.TeamNotFound);
                chat.Transfer(request.TeamId, request.MemberId, clock.GetUtcNow().UtcDateTime);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            return saved.IsFailure ? saved.Error : await RowAsync(id, cancellationToken);
        });

    public Task<Result<PagedResult<WhatsAppTemplateOptionResponse>>> ListTemplateOptionsAsync(WhatsAppInbox? inbox, Guid id,
        WhatsAppTemplateOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppTemplateOptionResponse>>(logger, clock, "PlatformWhatsAppConversationListTemplateOptions", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await responses.TemplateOptionsAsync(null, id, request, cancellationToken) is { } page ? page : WhatsAppErrors.ConversationNotFound;
        });

    /// <summary>Una plantilla «Para consultas» aprobada en la cuenta del número de Plataforma, en cualquier momento.</summary>
    public Task<Result<WhatsAppChatMessageResponse>> SendTemplateAsync(WhatsAppInbox? inbox, Guid id, SendWhatsAppTemplateRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppChatMessageResponse>(logger, clock, "PlatformWhatsAppConversationSendTemplate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var variables = request.Variables?.Select(value => value.Trim()).ToList() ?? [];
            return await unitOfWork.ExecuteInTransactionAsync<Result<WhatsAppChatMessageResponse>>(async ct =>
            {
                if (await chats.GetForUpdateAsync(id, null, ct) is not { } chat) return WhatsAppErrors.ConversationNotFound;
                var taken = await TakeAsync(chat, ct);
                if (taken.IsFailure) return taken.Error;
                var issued = await replies.IssueTemplateAsync(chat, platform: true, request.TemplateId!.Value, variables, ct);
                if (issued.IsFailure) return issued.Error;
                var nowUtc = clock.GetUtcNow().UtcDateTime;
                var message = PlatformWhatsAppChatMessage.Outbound(chat.Id, WhatsAppChatMessageKind.Template, Me, await MyNameAsync(ct), issued.Value.Text,
                    issued.Value.OutboxMessageId, issued.Value.TemplateId, nowUtc);
                chats.Add(message);
                chat.RecordOutbound(nowUtc, issued.Value.Text);
                return WhatsAppConversationService.Response(message);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<PagedResult<WhatsAppTransferTeamResponse>>> ListTransferTeamsAsync(WhatsAppInbox? inbox, Guid id,
        WhatsAppTransferOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppTransferTeamResponse>>(logger, clock, "PlatformWhatsAppConversationListTransferTeams", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await responses.RowAsync(null, id, cancellationToken) is null) return WhatsAppErrors.ConversationNotFound;
            var page = await teamReader.ListPlatformAsync(request, cancellationToken);
            var people = await teamPeople.PeopleAsync(page.Items.SelectMany(team => team.MemberIds), platform: true, cancellationToken);
            return new PagedResult<WhatsAppTransferTeamResponse>([.. page.Items.Select(team =>
                new WhatsAppTransferTeamResponse(team.Id, team.Name, team.MemberIds.Count(people.ContainsKey)))], page.Page, page.PageSize, page.TotalCount);
        });

    public Task<Result<PagedResult<WhatsAppTransferPersonResponse>>> ListTransferPeopleAsync(WhatsAppInbox? inbox, Guid id,
        WhatsAppTransferOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppTransferPersonResponse>>(logger, clock, "PlatformWhatsAppConversationListTransferPeople", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await responses.RowAsync(null, id, cancellationToken) is not { } row) return WhatsAppErrors.ConversationNotFound;
            var teamsOfChat = row.Team is { } team
                ? [.. await teamReader.FindPlatformAsync(team.Id, cancellationToken) is { } found ? [found] : Array.Empty<WhatsAppTeamRow>()]
                : (await teamReader.ListPlatformAsync(new WhatsAppTeamListRequest { PageSize = 100 }, cancellationToken)).Items;
            var people = await teamPeople.PeopleAsync(teamsOfChat.SelectMany(value => value.MemberIds), platform: true, cancellationToken);
            return await responses.PeopleAsync(null, people, request, cancellationToken);
        });

    public Task<Result<WhatsAppUntakenResponse>> GetUntakenAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppConversationGetUntaken",
            () => Task.FromResult(Result.Failure<WhatsAppUntakenResponse>(WhatsAppErrors.ConversationNotFound)));

    public Task<Result<PagedResult<WhatsAppReferenceResponse>>> ListInboxTeamsAsync(WhatsAppInbox? inbox, WhatsAppInboxTeamListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppReferenceResponse>>(logger, clock, "PlatformWhatsAppConversationInboxTeams", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await teamReader.ListPlatformAsync(request, cancellationToken);
            return new PagedResult<WhatsAppReferenceResponse>([.. page.Items.Select(team => new WhatsAppReferenceResponse(team.Id, team.Name))],
                page.Page, page.PageSize, page.TotalCount);
        });

    private Guid Me => user.UserId ?? throw new InvalidOperationException("An authenticated user is required.");

    private async Task<Result> TakeAsync(PlatformWhatsAppChat chat, CancellationToken ct)
    {
        if (chat.Take(Me, clock.GetUtcNow().UtcDateTime).IsSuccess) return Result.Success();
        var taker = chat.AssigneeUserId!.Value;
        return Result.Failure(WhatsAppErrors.ConversationTakenByOther(await responses.NameAsync(taker, ct) ?? string.Empty));
    }

    private Task<string?> MyNameAsync(CancellationToken ct) => responses.NameAsync(Me, ct);

    private async Task<Result<WhatsAppConversationRowResponse>> RowAsync(Guid id, CancellationToken ct) =>
        await responses.RowAsync(null, id, ct) is { } row ? row : WhatsAppErrors.ConversationNotFound;
}
