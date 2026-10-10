using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantPolicyService
{
    Task<Result<AssistantPolicyResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdateAsync(Guid tenantId, UpdateAssistantPolicyRequest request, CancellationToken cancellationToken);
}
