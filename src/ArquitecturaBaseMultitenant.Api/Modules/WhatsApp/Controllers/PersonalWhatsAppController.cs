using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;

/// <summary>«Mensajes» del perfil personal: conversaciones vinculadas, solo para leer, con «Seguir en WhatsApp».</summary>
[ApiController]
[Route("api/me/whatsapp")]
[Tags("WhatsApp Personal")]
[Access(Access.Consumer)]
[FeatureGate(WhatsAppFeature.Key)]
public sealed class PersonalWhatsAppController(IPersonalWhatsAppService service) : ControllerBase
{
    [HttpGet("conversations")]
    [ProducesResponseType<PagedResult<PersonalWhatsAppConversationResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new PersonalWhatsAppConversationListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("conversations/{id:guid}")]
    [ProducesResponseType<PersonalWhatsAppConversationDetailResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, [FromQuery] WhatsAppConversationMessagesHttpQuery query, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, new WhatsAppConversationMessagesRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
        }, cancellationToken)).ToActionResult(this);

    [HttpDelete("conversations/{id:guid}/link")]
    [ProducesResponseType(204)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Unlink([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.UnlinkAsync(id, cancellationToken)).ToActionResult(this);
}
