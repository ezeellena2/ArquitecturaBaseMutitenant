using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;

/// <summary>Difusiones solo a quienes aceptan promociones, con el costo estimado antes de mandar.</summary>
[ApiController]
[Route("api/organization/whatsapp/broadcasts")]
[Tags("WhatsApp Business broadcasts")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
[HasPermission(Permissions.WhatsApp.Broadcasts)]
public sealed class OrganizationWhatsAppBroadcastsController(IWhatsAppBroadcastService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WhatsAppBroadcastListResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new WhatsAppBroadcastListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("template-options")]
    [ProducesResponseType<PagedResult<WhatsAppTemplateOptionResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListTemplateOptions([FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListTemplateOptionsAsync(new WhatsAppTemplateOptionListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("tags")]
    [ProducesResponseType<PagedResult<string>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListTags([FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListTagsAsync(new WhatsAppTagListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WhatsAppBroadcastDetailResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, [FromQuery] WhatsAppBroadcastDetailHttpQuery query, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, new WhatsAppBroadcastDetailRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Status = query.Status,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("estimate")]
    [ProducesResponseType<WhatsAppBroadcastEstimateResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Estimate([FromQuery] WhatsAppBroadcastEstimateHttpQuery query, CancellationToken cancellationToken) =>
        (await service.EstimateAsync(new WhatsAppBroadcastEstimateRequest(query.TemplateId, query.Kind, query.Tag), cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<WhatsAppBroadcastResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Create([FromBody] CreateWhatsAppBroadcastHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CreateAsync(new CreateWhatsAppBroadcastRequest(request.Name, request.TemplateId, request.Variables, request.Audience, request.ScheduledAtUtc), cancellationToken))
        .ToCreatedResult(this, nameof(Get), broadcast => new { id = broadcast.Id });

    [HttpPost("{id:guid}/cancel")]
    [ResponseVersion("Version")]
    [ProducesResponseType<WhatsAppBroadcastResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Cancel([FromRoute] Guid id, [FromBody] CancelWhatsAppBroadcastHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CancelAsync(id, new CancelWhatsAppBroadcastRequest(request.Version), cancellationToken)).ToActionResult(this);

    [HttpPost("{id:guid}/duplicate")]
    [Idempotent]
    [ProducesResponseType<WhatsAppBroadcastResponse>(201)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Duplicate([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.DuplicateAsync(id, cancellationToken)).ToCreatedResult(this, nameof(Get), broadcast => new { id = broadcast.Id });
}
