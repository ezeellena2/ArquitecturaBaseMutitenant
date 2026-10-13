using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[ApiController]
[Route("api/organization/assistant/usage")]
[Tags("Assistant Business management")]
[Access(Access.Business)]
[FeatureGate("assistant")]
[HasPermission(Permissions.Assistant.Configure)]
public sealed class OrganizationAssistantUsageController(IAssistantUsageService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AssistantUsagePageResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListOrganization([FromQuery] OrganizationAssistantUsageHttpQuery request, CancellationToken cancellationToken) =>
        (await service.ListOrganizationAsync(new OrganizationAssistantUsageRequest(request.Month, request.GroupBy, request.Page, request.PageSize), cancellationToken)).ToActionResult(this);
}
