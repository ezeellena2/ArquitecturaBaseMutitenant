using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Api.Contracts.PublicSite;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ArquitecturaBaseMultitenant.Api.Controllers.PublicSite;

[ApiController]
[Route("api/public-site")]
[Tags("Sitio público")]
[PublicSite]
[AllowAnonymous]
public sealed class PublicSiteController(IPublicSiteService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PublicSiteHttpResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(cancellationToken);
        if (result.IsFailure) return result.ToActionResult(this);
        return Ok(PublicSiteHttpResponse.FromModel(result.Value));
    }

    [HttpGet("logo")]
    [ProducesResponseType<byte[]>(StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(CancellationToken cancellationToken)
    {
        var result = await service.GetLogoAsync(cancellationToken);
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
