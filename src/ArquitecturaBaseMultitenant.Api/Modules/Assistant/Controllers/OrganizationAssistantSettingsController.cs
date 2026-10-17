using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[ApiController]
[Route("api/organization/assistant/settings")]
[Tags("Assistant Business management")]
[Access(Access.Business)]
[FeatureGate("assistant")]
[HasPermission(Permissions.Assistant.Configure)]
public sealed class OrganizationAssistantSettingsController(IAssistantSettingsService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<OrganizationAssistantSettingsResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> GetOrganization(CancellationToken cancellationToken) =>
        (await service.GetOrganizationAsync(cancellationToken)).ToActionResult(this);

    [HttpPut]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> UpdateOrganization([FromBody] UpdateOrganizationAssistantSettingsHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateOrganizationAsync(new UpdateOrganizationAssistantSettingsRequest(request.Mode, request.ModelId, request.Effort, request.DailyMessageLimit, request.MonthlyBudget, request.KeyAction, request.ApiKey, request.Version), cancellationToken)).ToActionResult(this);
}
