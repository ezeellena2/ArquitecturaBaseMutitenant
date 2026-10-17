using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Los equipos de operadores de Plataforma (ADR 0038, precisión del 03/10): sin bandeja, globales y con su evento de
/// seguridad en la misma transacción. Borrar uno lo saca del bot y deja sus consultas sin equipo.
/// </summary>
internal sealed class PlatformWhatsAppTeamService(IPlatformWhatsAppTeamRepository teams, IWhatsAppTeamReader reader,
    IPlatformWhatsAppBotRepository bots, IPlatformWhatsAppChatRepository chats, WhatsAppTeamResponseBuilder responses,
    PlatformWhatsAppOperatorResolver operators, PlatformWhatsAppAuditWriter audit, IUnitOfWork unitOfWork, IRequestValidator validator,
    TimeProvider clock, ILogger<PlatformWhatsAppTeamService> logger) : IPlatformWhatsAppTeamService
{
    public Task<Result<PagedResult<WhatsAppTeamResponse>>> ListAsync(WhatsAppTeamListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppTeamResponse>>(logger, clock, "PlatformWhatsAppTeamList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListPlatformAsync(request, cancellationToken);
            return new PagedResult<WhatsAppTeamResponse>(await responses.RowsAsync(page.Items, platform: true, cancellationToken),
                page.Page, page.PageSize, page.TotalCount);
        });

    public Task<Result<WhatsAppTeamDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTeamDetailResponse>(logger, clock, "PlatformWhatsAppTeamGet", async () =>
            await reader.FindPlatformAsync(id, cancellationToken) is { } row
                ? await responses.DetailAsync(row, platform: true, cancellationToken) : WhatsAppErrors.TeamNotFound);

    public Task<Result<WhatsAppTeamResponse>> CreateAsync(CreateWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTeamResponse>(logger, clock, "PlatformWhatsAppTeamCreate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (!await operators.AreOperatorsAsync(request.MemberIds!, cancellationToken))
                return WhatsAppTeamService.Invalid("memberIds", "WhatsApp.Validation.SelectionInvalid");
            var team = PlatformWhatsAppTeam.Create(request.Name!, request.NotifyAfterMinutes, request.MemberIds!);
            var saved = await SaveAsync(ct =>
            {
                teams.Add(team);
                Record("WhatsApp platform team created", null, team.MemberIds.Count);
                return Task.FromResult(Result.Success());
            }, cancellationToken);
            return saved.IsFailure ? saved.Error : await ResponseAsync(team.Id, cancellationToken);
        });

    public Task<Result<WhatsAppTeamResponse>> UpdateAsync(Guid id, UpdateWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTeamResponse>(logger, clock, "PlatformWhatsAppTeamUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (!await operators.AreOperatorsAsync(request.MemberIds!, cancellationToken))
                return WhatsAppTeamService.Invalid("memberIds", "WhatsApp.Validation.SelectionInvalid");
            var saved = await SaveAsync(async ct =>
            {
                if (await teams.GetAsync(id, request.Version, ct) is not { } team) return Result.Failure(WhatsAppErrors.TeamNotFound);
                var before = team.MemberIds.Count;
                team.Update(request.Name!, request.NotifyAfterMinutes, request.MemberIds!);
                Record("WhatsApp platform team changed", before, team.MemberIds.Count);
                return Result.Success();
            }, cancellationToken);
            return saved.IsFailure ? saved.Error : await ResponseAsync(id, cancellationToken);
        });

    public Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppTeamDelete", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return Result.Failure(invalid);
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await teams.GetAsync(id, request.Version, ct) is not { } team) return Result.Failure(WhatsAppErrors.TeamNotFound);
                (await bots.GetAsync(null, ct))?.LeaveTeam(team.Id);
                foreach (var chat in await chats.ListByTeamAsync(team.Id, ct)) chat.LeaveTeam();
                teams.Remove(team);
                Record("WhatsApp platform team deleted", team.MemberIds.Count, null);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<PagedResult<WhatsAppReferenceResponse>>> ListMemberOptionsAsync(WhatsAppTeamMemberOptionListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppReferenceResponse>>(logger, clock, "PlatformWhatsAppTeamMemberOptions", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await operators.ListOptionsAsync(request, cancellationToken);
        });

    private void Record(string reason, int? before, int? after)
    {
        var changes = new SecurityEventChanges();
        changes.AddCount("whatsappTeamMembers", before, after);
        audit.Record(reason, changes);
    }

    private async Task<Result> SaveAsync(Func<CancellationToken, Task<Result>> work, CancellationToken ct)
    {
        try { return await unitOfWork.ExecuteInTransactionAsync(work, CommitPolicy.OnSuccess, ct); }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == WhatsAppUniqueKeys.PlatformTeamName)
        { return WhatsAppErrors.TeamNameTaken; }
    }

    private async Task<Result<WhatsAppTeamResponse>> ResponseAsync(Guid id, CancellationToken ct) =>
        await reader.FindPlatformAsync(id, ct) is { } row ? (await responses.RowsAsync([row], platform: true, ct))[0] : WhatsAppErrors.TeamNotFound;
}
