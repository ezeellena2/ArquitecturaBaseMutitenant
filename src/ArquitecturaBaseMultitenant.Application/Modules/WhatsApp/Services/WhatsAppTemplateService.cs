using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Las plantillas de la organización del claim (ADR 0038): se mandan a Meta en la cuenta de cada número vigente y Meta las
/// aprueba; una aprobada no se edita, se duplica desde el front. Borrar la quita también de Meta.
/// </summary>
internal sealed class WhatsAppTemplateService(IWhatsAppTemplateReader reader, IWhatsAppTemplateRepository templates, WhatsAppTemplateIssuer issuer,
    WhatsAppTemplateStateWriter states, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock,
    ILogger<WhatsAppTemplateService> logger) : IWhatsAppTemplateService
{
    public Task<Result<WhatsAppTemplateListResponse>> ListAsync(WhatsAppTemplateListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTemplateListResponse>(logger, clock, "WhatsAppTemplateList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListAsync(false, request, cancellationToken);
            return new WhatsAppTemplateListResponse(new PagedResult<WhatsAppTemplateResponse>([.. page.Items.Select(WhatsAppTemplateResponseBuilder.Response)],
                page.Page, page.PageSize, page.TotalCount), await reader.CountAsync(false, WhatsAppTemplateStatus.Approved, cancellationToken), null, null);
        });

    public Task<Result<WhatsAppTemplateResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTemplateResponse>(logger, clock, "WhatsAppTemplateGet", async () => await FindAsync(id, cancellationToken));

    public Task<Result<WhatsAppTemplateResponse>> CreateAsync(CreateWhatsAppTemplateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppTemplateResponse>(logger, clock, "WhatsAppTemplateCreate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var template = WhatsAppTemplate.Create(request.Name!, request.Language!, request.Category!.Value, request.Body!,
                request.Variables?.Select(variable => new WhatsAppTemplateVariable(variable.Label!.Trim(), variable.Example!.Trim())) ?? []);
            if (await reader.NameTakenAsync(false, template.MetaName, cancellationToken)) return WhatsAppErrors.TemplateNameTaken;
            var issued = await issuer.SubmitAsync(false, template, cancellationToken);
            if (issued.IsFailure) return issued.Error;
            try
            {
                var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    templates.Add(template);
                    foreach (var variant in issued.Value) await states.ApplyAsync(variant.WabaId, [variant.Meta], replaceAll: false, ct);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, cancellationToken);
                if (saved.IsFailure) return saved.Error;
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == WhatsAppUniqueKeys.TemplateName)
            { return WhatsAppErrors.TemplateNameTaken; }
            return await FindAsync(template.Id, cancellationToken);
        });

    public Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTemplateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTemplateDelete", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return Result.Failure(invalid);
            if (await reader.FindAsync(false, id, cancellationToken) is not { } row) return Result.Failure(WhatsAppErrors.TemplateNotFound);
            if (row.Version != request.Version) throw new ConcurrencyConflictException();
            var accounts = await issuer.RemoveAsync(false, row.MetaName, cancellationToken);
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await templates.GetAsync(id, request.Version, ct) is not { } template) return Result.Failure(WhatsAppErrors.TemplateNotFound);
                templates.Remove(template);
                await states.RemoveAsync(accounts, template.MetaName, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    private async Task<Result<WhatsAppTemplateResponse>> FindAsync(Guid id, CancellationToken ct) =>
        await reader.FindAsync(false, id, ct) is { } row ? WhatsAppTemplateResponseBuilder.Response(row) : WhatsAppErrors.TemplateNotFound;
}
