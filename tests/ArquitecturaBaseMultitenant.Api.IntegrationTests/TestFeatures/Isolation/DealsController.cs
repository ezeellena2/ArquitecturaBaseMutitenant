using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

[ApiController]
[Route("test/deals")]
[Access(Access.Consumer, Access.Business)]
[FeatureGate(EngagementFeatureCatalog.Deals)]
public sealed class DealsController : ControllerBase
{
    private readonly IDealService _service;
    public DealsController(IDealService service) => _service = service;

    [HttpGet]
    // Un módulo real agrega HasPermission("deals.read") a su bandeja Business; el catálogo productivo sigue vacío.
    public async Task<IActionResult> List([FromQuery] IsolationListHttpQuery query, CancellationToken cancellationToken) =>
        (await _service.ListAsync(new IsolationListRequest { Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await _service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Access(Access.Consumer)]
    [PublicSite]
    [Idempotent]
    public async Task<IActionResult> Create([FromBody] CreateDealHttpRequest request, CancellationToken cancellationToken) =>
        (await _service.CreateAsync(new CreateDealRequest(request.Summary), cancellationToken))
            .ToCreatedResult(this, nameof(Get), id => new { id });

    // Ambos accesos llegan al dominio para recibir Party.NotAllowed; PartyPolicy nunca concede la transición ajena.
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] TransitionDealHttpRequest request, CancellationToken cancellationToken) =>
        (await _service.TransitionAsync(new TransitionDealRequest(id, request.Version, DealStatus.Cancelled), cancellationToken))
            .ToActionResult(this);

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] TransitionDealHttpRequest request, CancellationToken cancellationToken) =>
        (await _service.TransitionAsync(new TransitionDealRequest(id, request.Version, DealStatus.Confirmed), cancellationToken))
            .ToActionResult(this);

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] TransitionDealHttpRequest request, CancellationToken cancellationToken) =>
        (await _service.TransitionAsync(new TransitionDealRequest(id, request.Version, DealStatus.Rejected), cancellationToken))
            .ToActionResult(this);
}
