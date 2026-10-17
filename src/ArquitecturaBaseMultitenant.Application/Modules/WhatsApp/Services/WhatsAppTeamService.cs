using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Los equipos de la organización del claim (ADR 0038): cada uno atiende una bandeja; sus integrantes son miembros activos
/// (hasta 200) y el PUT reemplaza la lista. Borrar un equipo lo saca del bot y deja sus conversaciones sin equipo.
/// </summary>
internal sealed class WhatsAppTeamService(IWhatsAppTeamRepository teams, IWhatsAppTeamReader reader, IWhatsAppBotRepository bots,
    IWhatsAppChatRepository chats, WhatsAppTeamResponseBuilder responses, IOrganizationUserReader users, IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<WhatsAppTeamService> logger) : IWhatsAppTeamService
{
    public Task<Result<PagedResult<WhatsAppTeamResponse>>> ListAsync(WhatsAppTeamListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppTeamResponse>>(logger, clock, "WhatsAppTeamList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListOrganizationAsync(null, request, cancellationToken);
            return new PagedResult<WhatsAppTeamResponse>(await responses.RowsAsync(page.Items, platform: false, cancellationToken),
                page.Page, page.PageSize, page.TotalCount);
        });

    public Task<Result<WhatsAppTeamDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTeamDetailResponse>(logger, clock, "WhatsAppTeamGet", async () =>
            await reader.FindOrganizationAsync(id, cancellationToken) is { } row
                ? await responses.DetailAsync(row, platform: false, cancellationToken) : WhatsAppErrors.TeamNotFound);

    public Task<Result<WhatsAppTeamResponse>> CreateAsync(CreateWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTeamResponse>(logger, clock, "WhatsAppTeamCreate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (request.Inbox is not { } inbox) return Invalid("inbox", "WhatsApp.Validation.Required");
            if (!await MembersAreActiveAsync(request.MemberIds!, cancellationToken)) return Invalid("memberIds", "WhatsApp.Validation.SelectionInvalid");
            var team = WhatsAppTeam.Create(request.Name!, inbox, request.NotifyAfterMinutes, request.MemberIds!);
            var saved = await SaveAsync(ct => { teams.Add(team); return Task.FromResult(Result.Success()); }, cancellationToken);
            return saved.IsFailure ? saved.Error : await ResponseAsync(team.Id, cancellationToken);
        });

    public Task<Result<WhatsAppTeamResponse>> UpdateAsync(Guid id, UpdateWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTeamResponse>(logger, clock, "WhatsAppTeamUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (!await MembersAreActiveAsync(request.MemberIds!, cancellationToken)) return Invalid("memberIds", "WhatsApp.Validation.SelectionInvalid");
            var saved = await SaveAsync(async ct =>
            {
                if (await teams.GetAsync(id, request.Version, ct) is not { } team) return Result.Failure(WhatsAppErrors.TeamNotFound);
                team.Update(request.Name!, request.NotifyAfterMinutes, request.MemberIds!);
                return Result.Success();
            }, cancellationToken);
            return saved.IsFailure ? saved.Error : await ResponseAsync(id, cancellationToken);
        });

    public Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTeamDelete", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return Result.Failure(invalid);
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await teams.GetAsync(id, request.Version, ct) is not { } team) return Result.Failure(WhatsAppErrors.TeamNotFound);
                foreach (var bot in await bots.ListAsync(ct)) bot.LeaveTeam(team.Id);
                foreach (var chat in await chats.ListByTeamAsync(team.Id, ct)) chat.LeaveTeam();
                teams.Remove(team);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<PagedResult<WhatsAppReferenceResponse>>> ListMemberOptionsAsync(WhatsAppTeamMemberOptionListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppReferenceResponse>>(logger, clock, "WhatsAppTeamMemberOptions", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await users.ListAsync(new ListOrganizationUsersRequest
            {
                Page = request.Page, PageSize = request.PageSize, Search = request.Search, State = OrganizationUserState.Active,
            }, clock.GetUtcNow().UtcDateTime, cancellationToken);
            return new PagedResult<WhatsAppReferenceResponse>([.. page.Items.Select(user => new WhatsAppReferenceResponse(user.MemberId, user.DisplayName))],
                page.Page, page.PageSize, page.TotalCount);
        });

    private async Task<bool> MembersAreActiveAsync(IReadOnlyList<Guid> memberIds, CancellationToken ct) =>
        (await reader.ListActiveMembersAsync(memberIds, ct)).Count == memberIds.Count;

    private async Task<Result> SaveAsync(Func<CancellationToken, Task<Result>> work, CancellationToken ct)
    {
        try { return await unitOfWork.ExecuteInTransactionAsync(work, CommitPolicy.OnSuccess, ct); }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == WhatsAppUniqueKeys.TeamName)
        { return WhatsAppErrors.TeamNameTaken; }
    }

    private async Task<Result<WhatsAppTeamResponse>> ResponseAsync(Guid id, CancellationToken ct) =>
        await reader.FindOrganizationAsync(id, ct) is { } row ? (await responses.RowsAsync([row], platform: false, ct))[0] : WhatsAppErrors.TeamNotFound;

    /// <summary>Un dato que el validador no puede ver (la bandeja la exige solo la organización; los integrantes, activos).</summary>
    internal static ValidationError Invalid(string field, string key) => new(new Dictionary<string, string[]> { [field] = [WhatsAppTexts.Get(key)] });
}
