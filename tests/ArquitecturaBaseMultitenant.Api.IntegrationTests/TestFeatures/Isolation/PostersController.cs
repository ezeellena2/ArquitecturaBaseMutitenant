using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

[ApiController]
[Route("test/posters")]
[PublicSite]
[AllowAnonymous]
public sealed class PostersController : ControllerBase
{
    private readonly IPosterService _service;
    public PostersController(IPosterService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] IsolationListHttpQuery query, CancellationToken cancellationToken) =>
        (await _service.ListAsync(new IsolationListRequest { Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await _service.GetAsync(id, cancellationToken)).ToActionResult(this);
}
