using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantUsageService
{
    Task<Result<AssistantUsagePageResponse>> ListOrganizationAsync(OrganizationAssistantUsageRequest request, CancellationToken cancellationToken);
    Task<Result<AssistantUsagePageResponse>> ListPlatformAsync(PlatformAssistantUsageRequest request, CancellationToken cancellationToken);
}
