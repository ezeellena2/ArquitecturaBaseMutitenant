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

[ApiController]
[Route("api/organization/whatsapp/numbers")]
[Tags("WhatsApp Business management")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
[HasPermission(Permissions.WhatsApp.Manage)]
public sealed class OrganizationWhatsAppNumbersController(IWhatsAppNumberService service, IWhatsAppBotService bots) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WhatsAppNumberListResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new WhatsAppNumberListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("company-options")]
    [ProducesResponseType<PagedResult<WhatsAppReferenceResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListCompanyOptions([FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListCompanyOptionsAsync(new WhatsAppCompanyOptionListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WhatsAppNumberResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<WhatsAppNumberResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(503)]
    public async Task<IActionResult> Connect([FromBody] ConnectWhatsAppNumberHttpRequest request, CancellationToken cancellationToken) =>
        (await service.ConnectAsync(new ConnectWhatsAppNumberRequest(request.Code, request.PhoneNumberId, request.WabaId, request.CompanyId, request.Coexistence), cancellationToken))
        .ToCreatedResult(this, nameof(Get), number => new { id = number.Id });

    [HttpPost("{id:guid}/check")]
    [ResponseVersion("Version")]
    [ProducesResponseType<WhatsAppNumberResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    [ProducesProblem(503)]
    public async Task<IActionResult> Check([FromRoute] Guid id, [FromBody] CheckWhatsAppNumberHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CheckAsync(id, new CheckWhatsAppNumberRequest(request.Version), cancellationToken)).ToActionResult(this);

    [HttpPut("{id:guid}")]
    [ResponseVersion("Version")]
    [ProducesResponseType<WhatsAppNumberResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateWhatsAppNumberHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(id, new UpdateWhatsAppNumberRequest(request.CompanyId, request.Version), cancellationToken)).ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Disconnect([FromRoute] Guid id, [FromQuery] WhatsAppVersionHttpQuery query, CancellationToken cancellationToken) =>
        (await service.DisconnectAsync(id, new DeleteWhatsAppNumberRequest(query.Version), cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}/bot")]
    [ProducesResponseType<WhatsAppBotResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> GetBot([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await bots.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPut("{id:guid}/bot")]
    [ResponseVersion("Version")]
    [ProducesResponseType<WhatsAppBotResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> UpdateBot([FromRoute] Guid id, [FromBody] UpdateWhatsAppBotHttpRequest request, CancellationToken cancellationToken) =>
        (await bots.UpdateAsync(id, new UpdateWhatsAppBotRequest(request.Welcome, request.OutOfHours, request.Schedule, request.Menu,
            request.AskPromotions, request.OfferAccountLink, request.EmployeesTeamId, request.Version), cancellationToken)).ToActionResult(this);
}
