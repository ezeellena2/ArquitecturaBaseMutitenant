using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/assistant/policy")]
[Tags("Assistant Platform management")]
[Access(Access.Platform)]
[InstalledModuleRoute(AssistantApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]
public sealed class PlatformAssistantPolicyController(IAssistantPolicyService service) : ControllerBase
{
    [HasPlatformPermission(PlatformPermissions.Tenants.Read)]
    [HttpGet]
    [ProducesResponseType<AssistantPolicyResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid tenantId, CancellationToken cancellationToken) =>
        (await service.GetAsync(tenantId, cancellationToken)).ToActionResult(this);

    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [HttpPut]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Update([FromRoute] Guid tenantId, [FromBody] UpdateAssistantPolicyHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(tenantId, new UpdateAssistantPolicyRequest(request.AllowOwnKey, request.Version, request.Reason), cancellationToken)).ToActionResult(this);
}
