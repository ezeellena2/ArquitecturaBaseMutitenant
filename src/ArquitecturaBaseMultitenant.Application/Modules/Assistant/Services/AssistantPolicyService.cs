using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantPolicyService(TimeProvider clock, ILogger<AssistantPolicyService> logger) : IAssistantPolicyService
{
    public Task<Result<AssistantPolicyResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantPolicyGet", () => Task.FromResult(Result.Failure<AssistantPolicyResponse>(ContractErrors.NotImplemented)));

    public Task<Result<VersionResponse>> UpdateAsync(Guid tenantId, UpdateAssistantPolicyRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantPolicyUpdate", () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));
}
