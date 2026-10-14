using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Las difusiones de la organización del claim (ADR 0038): una plantilla Marketing aprobada a los contactos que aceptan
/// promociones (todos o los de una etiqueta), con el costo estimado antes de mandar. Los destinatarios se fijan al crearla
/// y el consentimiento se vuelve a mirar al salir. Se puede cancelar mientras está programada.
/// </summary>
internal sealed class WhatsAppBroadcastService(IWhatsAppBroadcastReader reader, IWhatsAppBroadcastRepository broadcasts, IWhatsAppTemplateReader templates,
    IWhatsAppContactReader contacts, WhatsAppCostResolver costs, WhatsAppBroadcastChannelResolver channels, IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<WhatsAppBroadcastService> logger) : IWhatsAppBroadcastService
{
    public Task<Result<WhatsAppBroadcastListResponse>> ListAsync(WhatsAppBroadcastListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppBroadcastListResponse>(logger, clock, "WhatsAppBroadcastList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return new WhatsAppBroadcastListResponse(await reader.ListAsync(request, cancellationToken), await reader.CountScheduledAsync(cancellationToken));
        });

    public Task<Result<PagedResult<WhatsAppTemplateOptionResponse>>> ListTemplateOptionsAsync(WhatsAppTemplateOptionListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppTemplateOptionResponse>>(logger, clock, "WhatsAppBroadcastListTemplateOptions", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await templates.ListApprovedAsync(false, null, [WhatsAppTemplateCategory.Marketing], request, cancellationToken);
            var unit = page.Items.Count == 0 ? null : await costs.UnitAsync(null, WhatsAppTemplateCategory.Marketing, cancellationToken);
            return new PagedResult<WhatsAppTemplateOptionResponse>([.. page.Items.Select(template => WhatsAppTemplateResponseBuilder.Option(template, unit!))], page.Page, page.PageSize,
                page.TotalCount);
        });

    public Task<Result<PagedResult<string>>> ListTagsAsync(WhatsAppTagListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<string>>(logger, clock, "WhatsAppBroadcastListTags", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await contacts.ListTagsAsync(request, cancellationToken);
        });

    public Task<Result<WhatsAppBroadcastDetailResponse>> GetAsync(Guid id, WhatsAppBroadcastDetailRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppBroadcastDetailResponse>(logger, clock, "WhatsAppBroadcastGet", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await reader.FindAsync(id, cancellationToken) is not { } broadcast) return WhatsAppErrors.BroadcastNotFound;
            return new WhatsAppBroadcastDetailResponse(broadcast, await reader.ListRecipientsAsync(id, request, cancellationToken));
        });

    public Task<Result<WhatsAppBroadcastEstimateResponse>> EstimateAsync(WhatsAppBroadcastEstimateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppBroadcastEstimateResponse>(logger, clock, "WhatsAppBroadcastEstimate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var audience = await AudienceAsync(request.TemplateId!.Value, request.Tag, cancellationToken);
            if (audience.IsFailure) return audience.Error;
            var estimate = await costs.EstimateAsync([.. audience.Value.Members.Select(member => member.WaId)], WhatsAppTemplateCategory.Marketing, cancellationToken);
            return new WhatsAppBroadcastEstimateResponse(audience.Value.Members.Count, estimate.Cost, estimate.Country);
        });

    public Task<Result<WhatsAppBroadcastResponse>> CreateAsync(CreateWhatsAppBroadcastRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppBroadcastResponse>(logger, clock, "WhatsAppBroadcastCreate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            // Programada, tiene que ser para más adelante; sin fecha, sale enseguida.
            if (request.ScheduledAtUtc < clock.GetUtcNow().UtcDateTime) return WhatsAppTeamService.Invalid("scheduledAtUtc", "WhatsApp.Validation.ScheduleInPast");
            var audience = await AudienceAsync(request.TemplateId!.Value, request.Audience!.Tag, cancellationToken);
            if (audience.IsFailure) return audience.Error;
            var (template, members) = audience.Value;
            var variables = request.Variables?.Select(value => value.Trim()).ToList() ?? [];
            if (variables.Count != template.Variables.Count) return WhatsAppTeamService.Invalid("variables", "WhatsApp.Validation.VariablesInvalid");
            var estimate = await costs.EstimateAsync([.. members.Select(member => member.WaId)], WhatsAppTemplateCategory.Marketing, cancellationToken);
            var broadcast = WhatsAppBroadcast.Create(request.Name!, template.Id, template.Name, variables, request.Audience.Kind!.Value, request.Audience.Tag,
                request.ScheduledAtUtc, members.Count, estimate.Cost);
            var saved = await unitOfWork.ExecuteInTransactionAsync(ct =>
            {
                var nowUtc = clock.GetUtcNow().UtcDateTime;
                broadcasts.Add(broadcast);
                broadcasts.AddRecipients(members.Select(member => WhatsAppBroadcastRecipient.Create(broadcast.Id, member.Id, PhoneNumber.Create(member.WaId).Value, nowUtc)));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            return await reader.FindAsync(broadcast.Id, cancellationToken) is { } response ? response : WhatsAppErrors.BroadcastNotFound;
        });

    public Task<Result<WhatsAppBroadcastResponse>> CancelAsync(Guid id, CancelWhatsAppBroadcastRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppBroadcastResponse>(logger, clock, "WhatsAppBroadcastCancel", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
                await broadcasts.GetForUpdateAsync(id, request.Version, ct) is { } broadcast ? broadcast.Cancel() : Result.Failure(WhatsAppErrors.BroadcastNotFound),
                CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            return await reader.FindAsync(id, cancellationToken) is { } response ? response : WhatsAppErrors.BroadcastNotFound;
        });

    /// <summary>La plantilla tiene que ser Marketing, estar aprobada y tener un número que la mande; y alguien tiene que aceptar promociones.</summary>
    private async Task<Result<(WhatsAppTemplateRow Template, IReadOnlyList<WhatsAppAudienceMember> Members)>> AudienceAsync(Guid templateId, string? tag,
        CancellationToken ct)
    {
        if (await templates.FindAsync(false, templateId, ct) is not { Status: WhatsAppTemplateStatus.Approved } template) return WhatsAppErrors.TemplateNotApproved;
        var channel = await channels.ResolveAsync(template, ct);
        if (channel.IsFailure) return channel.Error;
        var members = await contacts.ListAudienceAsync(tag, ct);
        return members.Count == 0 ? WhatsAppErrors.BroadcastNoRecipients : (template, members);
    }
}
