using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// «Mensajes» de la persona (ADR 0038, D5): sus conversaciones vinculadas, para leer, desde que las vinculó. Desvincular la
/// borra de las dos partes; un vínculo ajeno no existe (404).
/// </summary>
internal sealed class PersonalWhatsAppService(IWhatsAppConversationLinkReader reader, IWhatsAppConversationLinkRepository links,
    ITenantContext tenant, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock, ILogger<PersonalWhatsAppService> logger)
    : IPersonalWhatsAppService
{
    public Task<Result<PagedResult<PersonalWhatsAppConversationResponse>>> ListAsync(PersonalWhatsAppConversationListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<PersonalWhatsAppConversationResponse>>(logger, clock, "PersonalWhatsAppList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListAsync(request, cancellationToken);
            return new PagedResult<PersonalWhatsAppConversationResponse>([.. page.Items.Select(row => row.Response())], page.Page, page.PageSize,
                page.TotalCount);
        });

    public Task<Result<PersonalWhatsAppConversationDetailResponse>> GetAsync(Guid id, WhatsAppConversationMessagesRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PersonalWhatsAppConversationDetailResponse>(logger, clock, "PersonalWhatsAppGet", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await reader.FindAsync(id, cancellationToken) is not { } conversation) return WhatsAppErrors.LinkNotFound;
            return new PersonalWhatsAppConversationDetailResponse(conversation.Response(), await reader.ListMessagesAsync(id, request, cancellationToken));
        });

    public Task<Result> UnlinkAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PersonalWhatsAppUnlink", () =>
            unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await links.GetAsync(id, ct) is not { } link
                    || PartyPolicy.Require(link, Party.Consumer, tenant.RequiredTenantId, Party.Consumer).IsFailure)
                    return Result.Failure(WhatsAppErrors.LinkNotFound);
                links.Remove(link);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken));
}
