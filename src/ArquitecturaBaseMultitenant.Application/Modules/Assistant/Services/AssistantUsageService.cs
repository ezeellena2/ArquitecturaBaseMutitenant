using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantUsageService(TimeProvider clock, ILogger<AssistantUsageService> logger) : IAssistantUsageService
{
    public Task<Result<AssistantUsagePageResponse>> ListOrganizationAsync(OrganizationAssistantUsageRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantUsageListOrganization", () => Task.FromResult(Result.Failure<AssistantUsagePageResponse>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantUsagePageResponse>> ListPlatformAsync(PlatformAssistantUsageRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantUsageListPlatform", () => Task.FromResult(Result.Failure<AssistantUsagePageResponse>(ContractErrors.NotImplemented)));
}
