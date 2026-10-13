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

/// <summary>Contactos con su consentimiento de promociones: «Atender clientes» los ve, edita e importa (D3).</summary>
[ApiController]
[Route("api/organization/whatsapp/contacts")]
[Tags("WhatsApp Business contacts")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
[HasPermission(Permissions.WhatsApp.Customers)]
public sealed class OrganizationWhatsAppContactsController(IWhatsAppContactService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WhatsAppContactListResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] WhatsAppContactListHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new WhatsAppContactListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
            Tag = query.Tag,
            Consent = query.Consent,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WhatsAppContactResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}/conversations")]
    [ProducesResponseType<PagedResult<WhatsAppContactConversationResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListConversations([FromRoute] Guid id, [FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListConversationsAsync(id, new WhatsAppContactChildListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}/broadcasts")]
    [ProducesResponseType<PagedResult<WhatsAppContactBroadcastResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListBroadcasts([FromRoute] Guid id, [FromQuery] WhatsAppPagedHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListBroadcastsAsync(id, new WhatsAppContactChildListRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);

    [HttpPut("{id:guid}")]
    [ResponseVersion("Version")]
    [ProducesResponseType<WhatsAppContactResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateWhatsAppContactHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(id, new UpdateWhatsAppContactRequest(request.Name, request.Tags, request.Consent, request.Version), cancellationToken))
        .ToActionResult(this);

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

    [HttpGet("import-sample")]
    [ProducesResponseType<byte[]>(200, "application/octet-stream")]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> GetImportSample(CancellationToken cancellationToken) =>
        (await service.GetImportSampleAsync(cancellationToken)).ToFileResult(this);

    [HttpPost("import/preview")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<WhatsAppImportPreviewResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(413)]
    [ProducesProblem(500)]
    public async Task<IActionResult> PreviewImport([FromForm] ImportWhatsAppContactsHttpRequest request, CancellationToken cancellationToken) =>
        (await service.PreviewImportAsync(new ImportWhatsAppContactsRequest(request.File?.FileName, request.File?.ContentType, request.File?.OpenReadStream(), request.Tag), cancellationToken))
        .ToActionResult(this);

    [HttpPost("import")]
    [Idempotent]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<WhatsAppImportResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(413)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Import([FromForm] ImportWhatsAppContactsHttpRequest request, CancellationToken cancellationToken) =>
        (await service.ImportAsync(new ImportWhatsAppContactsRequest(request.File?.FileName, request.File?.ContentType, request.File?.OpenReadStream(), request.Tag), cancellationToken))
        .ToActionResult(this);
}
