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
/// Las plantillas de Plataforma (ADR 0038): «Para consultas» se mandan a Meta en la cuenta del número de Plataforma y son
/// solo Utility (Plataforma no tiene marketing); las 16 «Del sistema» son de la E8, se leen con su estado en Meta y no se
/// borran. Cada cambio deja su evento de seguridad.
/// </summary>
internal sealed class PlatformWhatsAppTemplateService(IWhatsAppTemplateReader reader, IPlatformWhatsAppTemplateRepository templates,
    WhatsAppTemplateIssuer issuer, WhatsAppTemplateStateWriter states, WhatsAppTemplateResponseBuilder responses, PlatformWhatsAppAuditWriter audit,
    IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock, ILogger<PlatformWhatsAppTemplateService> logger) : IPlatformWhatsAppTemplateService
{
    public Task<Result<WhatsAppTemplateListResponse>> ListAsync(WhatsAppTemplateListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTemplateListResponse>(logger, clock, "PlatformWhatsAppTemplateList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var system = await responses.SystemAsync(cancellationToken);
            var consultations = await reader.CountAsync(true, null, cancellationToken);
            if (request.Group == WhatsAppTemplateGroup.System)
            {
                var search = request.Search?.Trim();
                var shown = system.Where(template => (request.Status is null || template.Status == request.Status) && (string.IsNullOrEmpty(search)
                    || template.Title!.Contains(search, StringComparison.CurrentCultureIgnoreCase) || template.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(template => template.Title, StringComparer.CurrentCulture).ToList();
                return new WhatsAppTemplateListResponse(new PagedResult<WhatsAppTemplateResponse>([.. shown.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)],
                    request.Page, request.PageSize, shown.Count), system.Count(template => template.Status == WhatsAppTemplateStatus.Approved), consultations, system.Count);
            }
            var page = await reader.ListAsync(true, request, cancellationToken);
            return new WhatsAppTemplateListResponse(new PagedResult<WhatsAppTemplateResponse>([.. page.Items.Select(WhatsAppTemplateResponseBuilder.Response)],
                page.Page, page.PageSize, page.TotalCount), await reader.CountAsync(true, WhatsAppTemplateStatus.Approved, cancellationToken),
                consultations, system.Count);
        });

    public Task<Result<WhatsAppTemplateResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTemplateResponse>(logger, clock, "PlatformWhatsAppTemplateGet", async () =>
            (await responses.SystemAsync(cancellationToken)).FirstOrDefault(template => template.Id == id) is { } system
                ? system : await FindAsync(id, cancellationToken));

    public Task<Result<WhatsAppTemplateResponse>> CreateAsync(CreateWhatsAppTemplateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTemplateResponse>(logger, clock, "PlatformWhatsAppTemplateCreate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (request.Category != WhatsAppTemplateCategory.Utility) return WhatsAppTeamService.Invalid("category", "WhatsApp.Validation.SelectionInvalid");
            var template = PlatformWhatsAppTemplate.Create(request.Name!, request.Language!, request.Body!,
                request.Variables?.Select(variable => new WhatsAppTemplateVariable(variable.Label!.Trim(), variable.Example!.Trim())) ?? []);
            if (await reader.NameTakenAsync(true, template.MetaName, cancellationToken)) return WhatsAppErrors.TemplateNameTaken;
            var issued = await issuer.SubmitAsync(true, template, cancellationToken);
            if (issued.IsFailure) return issued.Error;
            try
            {
                var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    templates.Add(template);
                    foreach (var variant in issued.Value) await states.ApplyAsync(variant.WabaId, [variant.Meta], replaceAll: false, ct);
                    Record("WhatsApp template created", 0, 1);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, cancellationToken);
                if (saved.IsFailure) return saved.Error;
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == WhatsAppUniqueKeys.PlatformTemplateName)
            { return WhatsAppErrors.TemplateNameTaken; }
            return await FindAsync(template.Id, cancellationToken);
        });

    public Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTemplateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppTemplateDelete", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return Result.Failure(invalid);
            // Las del sistema no tienen fila: no se encuentran para borrar.
            if (await reader.FindAsync(true, id, cancellationToken) is not { } row) return Result.Failure(WhatsAppErrors.TemplateNotFound);
            if (row.Version != request.Version) throw new ConcurrencyConflictException();
            var accounts = await issuer.RemoveAsync(true, row.MetaName, cancellationToken);
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await templates.GetAsync(id, request.Version, ct) is not { } template) return Result.Failure(WhatsAppErrors.TemplateNotFound);
                templates.Remove(template);
                await states.RemoveAsync(accounts, template.MetaName, ct);
                Record("WhatsApp template deleted", 1, 0);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    private void Record(string reason, int before, int after)
    {
        var changes = new SecurityEventChanges();
        changes.AddCount("whatsappTemplates", before, after);
        audit.Record(reason, changes);
    }

    private async Task<Result<WhatsAppTemplateResponse>> FindAsync(Guid id, CancellationToken ct) =>
        await reader.FindAsync(true, id, ct) is { } row ? WhatsAppTemplateResponseBuilder.Response(row) : WhatsAppErrors.TemplateNotFound;
}
