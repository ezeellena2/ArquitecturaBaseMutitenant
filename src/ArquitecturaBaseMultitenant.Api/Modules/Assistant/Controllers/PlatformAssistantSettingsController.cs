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
[Route("api/platform/assistant/settings")]
[Tags("Assistant Platform management")]
[Access(Access.Platform)]
[InstalledModuleRoute(AssistantApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]
[HasPlatformPermission(PlatformPermissions.Assistant.Configure)]
public sealed class PlatformAssistantSettingsController(IAssistantSettingsService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformAssistantSettingsResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> GetPlatform(CancellationToken cancellationToken) =>
        (await service.GetPlatformAsync(cancellationToken)).ToActionResult(this);

    [HttpPut]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> UpdatePlatform([FromBody] UpdatePlatformAssistantSettingsHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdatePlatformAsync(new UpdatePlatformAssistantSettingsRequest(request.DefaultModelId, request.AllowedModelIds, request.Effort, request.DailyMessageLimit, request.MonthlyOrganizationBudget, request.PersonalEnabled, request.RetentionDays, request.KeyAction, request.ApiKey, request.Version), cancellationToken)).ToActionResult(this);
}
