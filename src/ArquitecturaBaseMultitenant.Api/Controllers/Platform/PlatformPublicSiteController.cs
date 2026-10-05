using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants/{id:guid}/public-site")]
[Tags("Moderación de páginas públicas")]
[Access(Access.Platform)]
[HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
public sealed class PlatformPublicSiteController(IPlatformPublicSiteService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformPublicSiteResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost("unpublish")]
    [Idempotent]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unpublish(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UnpublishAsync(id, request.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpPost("allow-publish")]
    [Idempotent]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AllowPublish(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken cancellationToken) =>
        (await service.AllowPublishAsync(id, request.ToModel(), cancellationToken)).ToActionResult(this);
}
