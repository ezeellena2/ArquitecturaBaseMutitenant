using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSettingsService(TimeProvider clock, ILogger<AssistantSettingsService> logger) : IAssistantSettingsService
{
    public Task<Result<OrganizationAssistantSettingsResponse>> GetOrganizationAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantSettingsGetOrganization", () => Task.FromResult(Result.Failure<OrganizationAssistantSettingsResponse>(ContractErrors.NotImplemented)));

    public Task<Result<VersionResponse>> UpdateOrganizationAsync(UpdateOrganizationAssistantSettingsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantSettingsUpdateOrganization", () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PlatformAssistantSettingsResponse>> GetPlatformAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantSettingsGetPlatform", () => Task.FromResult(Result.Failure<PlatformAssistantSettingsResponse>(ContractErrors.NotImplemented)));

    public Task<Result<VersionResponse>> UpdatePlatformAsync(UpdatePlatformAssistantSettingsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantSettingsUpdatePlatform", () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));
}
