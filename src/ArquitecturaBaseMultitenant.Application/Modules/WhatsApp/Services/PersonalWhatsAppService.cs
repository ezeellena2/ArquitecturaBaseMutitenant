using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class PersonalWhatsAppService(TimeProvider clock, ILogger<PersonalWhatsAppService> logger) : IPersonalWhatsAppService
{
    public Task<Result<PagedResult<PersonalWhatsAppConversationResponse>>> ListAsync(PersonalWhatsAppConversationListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PersonalWhatsAppList", () => Task.FromResult(Result.Failure<PagedResult<PersonalWhatsAppConversationResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<PersonalWhatsAppConversationDetailResponse>> GetAsync(Guid id, WhatsAppConversationMessagesRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PersonalWhatsAppGet", () => Task.FromResult(Result.Failure<PersonalWhatsAppConversationDetailResponse>(ContractErrors.NotImplemented)));

    public Task<Result> UnlinkAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PersonalWhatsAppUnlink", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));
}
