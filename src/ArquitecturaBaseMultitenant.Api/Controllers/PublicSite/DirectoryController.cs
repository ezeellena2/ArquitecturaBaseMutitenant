using ArquitecturaBaseMultitenant.Api.Contracts.PublicSite;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ArquitecturaBaseMultitenant.Api.Controllers.PublicSite;

[ApiController]
[Route("api/directory")]
[Tags("Sitio público")]
[AllowAnonymous]
public sealed class DirectoryController(IDirectoryService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<DirectoryEntryRow>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] DirectoryQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new ListDirectoryRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Sort = query.Sort,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{slug}/logo")]
    [ProducesResponseType<byte[]>(StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo([FromRoute] string slug, CancellationToken cancellationToken)
    {
        var result = await service.GetLogoAsync(slug, cancellationToken);
        if (result.IsFailure) return result.ToActionResult(this);

        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
        var logo = result.Value;
        return new FileContentResult(logo.Bytes, logo.ContentType)
        {
            EntityTag = new EntityTagHeaderValue("\"public-page-"
                + VersionWireFormatter.Format(logo.Version) + "\""),
        };
    }
}
