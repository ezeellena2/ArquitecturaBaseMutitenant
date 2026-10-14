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

/// <summary>Equipos: quiénes reciben los chats y cuándo se avisa por correo. El servicio separa organización y Plataforma.</summary>
[ApiController]
public abstract class WhatsAppTeamsControllerBase(IWhatsAppTeamService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<WhatsAppTeamResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new WhatsAppTeamListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("member-options")]
    [ProducesResponseType<PagedResult<WhatsAppReferenceResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListMemberOptions([FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListMemberOptionsAsync(new WhatsAppTeamMemberOptionListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WhatsAppTeamDetailResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<WhatsAppTeamResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Create([FromBody] CreateWhatsAppTeamHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CreateAsync(new CreateWhatsAppTeamRequest(request.Name, request.Inbox, request.MemberIds, request.NotifyAfterMinutes), cancellationToken))
        .ToCreatedResult(this, nameof(Get), team => new { id = team.Id });

    [HttpPut("{id:guid}")]
    [ResponseVersion("Version")]
    [ProducesResponseType<WhatsAppTeamResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateWhatsAppTeamHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(id, new UpdateWhatsAppTeamRequest(request.Name, request.MemberIds, request.NotifyAfterMinutes, request.Version), cancellationToken))
        .ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, [FromQuery] WhatsAppVersionHttpQuery query, CancellationToken cancellationToken) =>
        (await service.DeleteAsync(id, new DeleteWhatsAppTeamRequest(query.Version), cancellationToken)).ToActionResult(this);
}

[Route("api/organization/whatsapp/teams")]
[Tags("WhatsApp Business management")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
[HasPermission(Permissions.WhatsApp.Manage)]
public sealed class OrganizationWhatsAppTeamsController(IWhatsAppTeamService service) : WhatsAppTeamsControllerBase(service);

[Route("api/platform/whatsapp/teams")]
[Tags("WhatsApp Platform management")]
[Access(Access.Platform)]
[InstalledModuleRoute(WhatsAppApiModule.ManagementRoute, InstalledModuleRouteCategory.PlatformManagement)]
[HasPlatformPermission(PlatformPermissions.WhatsApp.Manage)]
public sealed class PlatformWhatsAppTeamsController(IPlatformWhatsAppTeamService service) : WhatsAppTeamsControllerBase(service);
