using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

[ApiController]
[Route("api/organization/public-page")]
[Tags("Página pública de la organización")]
[Access(Access.Business)]
public sealed class PublicPageController(IPublicPageService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.PublicPage.Manage)]
    [ProducesResponseType<PublicPageHttpResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(cancellationToken);
        return result.IsFailure ? result.ToActionResult(this) : Ok(result.Value.ToHttpResponse());
    }

    [HttpPut]
    [HasPermission(Permissions.PublicPage.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] UpdatePublicPageHttpRequest request,
        CancellationToken cancellationToken)
    {
        var model = request.ToModel();
        if (model.IsFailure) return model.ToActionResult(this);
        return (await service.UpdateAsync(model.Value, cancellationToken)).ToActionResult(this);
    }

    [HttpPost("publish")]
    [HasPermission(Permissions.PublicPage.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish([FromBody] SetPublicPagePublicationHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.PublishAsync(request.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpPost("unpublish")]
    [HasPermission(Permissions.PublicPage.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unpublish([FromBody] SetPublicPagePublicationHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.UnpublishAsync(request.ToModel(), cancellationToken)).ToActionResult(this);
}
