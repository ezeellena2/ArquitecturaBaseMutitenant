using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;

/// <summary>Las acciones de una bandeja; cada derivada declara ruta, acceso, permiso y su bandeja.</summary>
[ApiController]
public abstract class WhatsAppConversationsControllerBase(IWhatsAppConversationService service) : ControllerBase
{
    protected abstract WhatsAppInbox? Inbox { get; }

    [HttpGet("conversations")]
    [ProducesResponseType<WhatsAppConversationListResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] WhatsAppConversationListHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(Inbox, new WhatsAppConversationListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
            TeamId = query.TeamId,
            State = query.State,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("conversations/{id:guid}")]
    [ProducesResponseType<WhatsAppConversationResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, [FromQuery] WhatsAppConversationMessagesHttpQuery query, CancellationToken cancellationToken) =>
        (await service.GetAsync(Inbox, id, new WhatsAppConversationMessagesRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
        }, cancellationToken)).ToActionResult(this);

    [HttpPost("conversations/{id:guid}/take")]
    [ProducesResponseType<WhatsAppConversationRowResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Take([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.TakeAsync(Inbox, id, cancellationToken)).ToActionResult(this);

    [HttpPost("conversations/{id:guid}/messages")]
    [Idempotent]
    [ProducesResponseType<WhatsAppChatMessageResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Send([FromRoute] Guid id, [FromBody] SendWhatsAppMessageHttpRequest request, CancellationToken cancellationToken) =>
        (await service.SendAsync(Inbox, id, new SendWhatsAppMessageRequest(request.Text), cancellationToken))
        .ToCreatedResult(this, nameof(Get), _ => new { id });

    [HttpPost("conversations/{id:guid}/notes")]
    [Idempotent]
    [ProducesResponseType<WhatsAppChatMessageResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> AddNote([FromRoute] Guid id, [FromBody] AddWhatsAppNoteHttpRequest request, CancellationToken cancellationToken) =>
        (await service.AddNoteAsync(Inbox, id, new AddWhatsAppNoteRequest(request.Text), cancellationToken))
        .ToCreatedResult(this, nameof(Get), _ => new { id });

    [HttpPost("conversations/{id:guid}/transfer")]
    [ResponseVersion("Version")]
    [ProducesResponseType<WhatsAppConversationRowResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Transfer([FromRoute] Guid id, [FromBody] TransferWhatsAppConversationHttpRequest request, CancellationToken cancellationToken) =>
        (await service.TransferAsync(Inbox, id, new TransferWhatsAppConversationRequest(request.TeamId, request.MemberId, request.Version), cancellationToken))
        .ToActionResult(this);

    [HttpGet("conversations/{id:guid}/template-options")]
    [ProducesResponseType<PagedResult<WhatsAppTemplateOptionResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListTemplateOptions([FromRoute] Guid id, [FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListTemplateOptionsAsync(Inbox, id, new WhatsAppTemplateOptionListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpPost("conversations/{id:guid}/templates")]
    [Idempotent]
    [ProducesResponseType<WhatsAppChatMessageResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> SendTemplate([FromRoute] Guid id, [FromBody] SendWhatsAppTemplateHttpRequest request, CancellationToken cancellationToken) =>
        (await service.SendTemplateAsync(Inbox, id, new SendWhatsAppTemplateRequest(request.TemplateId, request.Variables), cancellationToken))
        .ToCreatedResult(this, nameof(Get), _ => new { id });
}
