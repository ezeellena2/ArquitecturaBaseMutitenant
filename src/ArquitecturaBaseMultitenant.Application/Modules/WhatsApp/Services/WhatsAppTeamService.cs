using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppTeamService(TimeProvider clock, ILogger<WhatsAppTeamService> logger) : IWhatsAppTeamService
{
    public Task<Result<PagedResult<WhatsAppTeamResponse>>> ListAsync(WhatsAppTeamListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTeamList", () => Task.FromResult(Result.Failure<PagedResult<WhatsAppTeamResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppTeamDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTeamGet", () => Task.FromResult(Result.Failure<WhatsAppTeamDetailResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppTeamResponse>> CreateAsync(CreateWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTeamCreate", () => Task.FromResult(Result.Failure<WhatsAppTeamResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppTeamResponse>> UpdateAsync(Guid id, UpdateWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTeamUpdate", () => Task.FromResult(Result.Failure<WhatsAppTeamResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PagedResult<WhatsAppReferenceResponse>>> ListMemberOptionsAsync(WhatsAppTeamMemberOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTeamMemberOptions", () => Task.FromResult(Result.Failure<PagedResult<WhatsAppReferenceResponse>>(ContractErrors.NotImplemented)));

    public Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTeamRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTeamDelete", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));
}
