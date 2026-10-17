using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[ApiController]
[Route("api/platform/assistant/models")]
[Tags("Assistant Platform management")]
[Access(Access.Platform)]
[InstalledModuleRoute(AssistantApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]
[HasPlatformPermission(PlatformPermissions.Assistant.Configure)]
public sealed class PlatformAssistantModelsController(IAssistantModelService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AssistantModelPriceResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] AssistantModelListHttpQuery request, CancellationToken cancellationToken) =>
        (await service.ListAsync(new AssistantModelListRequest(request.Page, request.PageSize, request.Search, request.Active), cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent(Mode = IdempotencyMode.BufferedJson)]
    [ProducesResponseType<AssistantCreatedModelResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Create([FromBody] CreateAssistantModelHttpRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateAssistantModelRequest(request.Name, request.Provider, request.ProviderModelId, request.InputPricePerMillion, request.OutputPricePerMillion, request.IsActive, request.Technical?.ToModel()), cancellationToken);
        return result.IsSuccess ? StatusCode(201, result.Value) : result.ToActionResult(this);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateAssistantModelHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(id, new UpdateAssistantModelRequest(request.Name, request.Provider, request.ProviderModelId, request.InputPricePerMillion, request.OutputPricePerMillion, request.IsActive, request.Version, request.Technical?.ToModel()), cancellationToken)).ToActionResult(this);
}
