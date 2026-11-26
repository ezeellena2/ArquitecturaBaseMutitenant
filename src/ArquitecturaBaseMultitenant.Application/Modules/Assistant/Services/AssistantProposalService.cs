using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Contrato de las acciones del asistente (K1): las tres rutas de la propuesta responden 501 hasta que la B5 las programe.</summary>
internal sealed class AssistantProposalService(TimeProvider clock, ILogger<AssistantProposalService> logger) : IAssistantProposalService
{
    public Task<Result<AssistantProposalResponse>> GetAsync(Guid proposalId, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantProposalGet",
            () => Task.FromResult(Result.Failure<AssistantProposalResponse>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantProposalAttemptResponse>> AttemptAsync(Guid proposalId, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantProposalAttempt",
            () => Task.FromResult(Result.Failure<AssistantProposalAttemptResponse>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantProposalResponse>> AppliedAsync(Guid proposalId, AssistantProposalAppliedRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantProposalApplied",
            () => Task.FromResult(Result.Failure<AssistantProposalResponse>(ContractErrors.NotImplemented)));
}
