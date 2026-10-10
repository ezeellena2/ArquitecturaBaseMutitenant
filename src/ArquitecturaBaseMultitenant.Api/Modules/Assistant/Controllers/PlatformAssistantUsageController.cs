using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[ApiController]
[Route("api/platform/assistant/usage")]
[Tags("Assistant Platform management")]
[Access(Access.Platform)]
[InstalledModuleRoute(AssistantApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]
[HasPlatformPermission(PlatformPermissions.Assistant.Read)]
public sealed class PlatformAssistantUsageController(IAssistantUsageService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AssistantUsagePageResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> ListPlatform([FromQuery] PlatformAssistantUsageHttpQuery request, CancellationToken cancellationToken) =>
        (await service.ListPlatformAsync(new PlatformAssistantUsageRequest(request.Month, request.Page, request.PageSize), cancellationToken)).ToActionResult(this);
}
