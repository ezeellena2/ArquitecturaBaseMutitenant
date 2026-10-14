using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
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

/// <summary>Plantillas que crea la organización y aprueba Meta. Una aprobada no se edita: se duplica.</summary>
[ApiController]
[Route("api/organization/whatsapp/templates")]
[Tags("WhatsApp Business management")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
[HasPermission(Permissions.WhatsApp.Manage)]
public sealed class OrganizationWhatsAppTemplatesController(IWhatsAppTemplateService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WhatsAppTemplateListResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] WhatsAppTemplateListHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new WhatsAppTemplateListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
            Status = query.Status,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WhatsAppTemplateResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<WhatsAppTemplateResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(503)]
    public async Task<IActionResult> Create([FromBody] CreateWhatsAppTemplateHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CreateAsync(new CreateWhatsAppTemplateRequest(request.Name, request.Category, request.Language, request.Body, request.Variables), cancellationToken))
        .ToCreatedResult(this, nameof(Get), template => new { id = template.Id });

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, [FromQuery] WhatsAppVersionHttpQuery query, CancellationToken cancellationToken) =>
        (await service.DeleteAsync(id, new DeleteWhatsAppTemplateRequest(query.Version), cancellationToken)).ToActionResult(this);
}

/// <summary>Plantillas de Plataforma: «Para consultas», que se crean y duplican, y las 16 «Del sistema», solo de lectura.</summary>
[ApiController]
[Route("api/platform/whatsapp/templates")]
[Tags("WhatsApp Platform management")]
[Access(Access.Platform)]
[InstalledModuleRoute(WhatsAppApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]
[HasPlatformPermission(PlatformPermissions.WhatsApp.Manage)]
public sealed class PlatformWhatsAppTemplatesController(IWhatsAppTemplateService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WhatsAppTemplateListResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] PlatformWhatsAppTemplateListHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new WhatsAppTemplateListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
            Status = query.Status,
            Group = query.Group,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WhatsAppTemplateResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<WhatsAppTemplateResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(503)]
    public async Task<IActionResult> Create([FromBody] CreateWhatsAppTemplateHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CreateAsync(new CreateWhatsAppTemplateRequest(request.Name, request.Category, request.Language, request.Body, request.Variables), cancellationToken))
        .ToCreatedResult(this, nameof(Get), template => new { id = template.Id });

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, [FromQuery] WhatsAppVersionHttpQuery query, CancellationToken cancellationToken) =>
        (await service.DeleteAsync(id, new DeleteWhatsAppTemplateRequest(query.Version), cancellationToken)).ToActionResult(this);
}
