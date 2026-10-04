using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/legal-documents")]
[Tags("Documentos legales de Plataforma")]
[Access(Access.Platform)]
[HasPlatformPermission(PlatformPermissions.Legal.Manage)]
public sealed class PlatformLegalDocumentsController(IPlatformLegalService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlatformLegalDocumentGroupResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        (await service.ListAsync(ct)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlatformLegalDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Find(Guid id, CancellationToken ct) =>
        (await service.FindAsync(id, ct)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<PublishedLegalDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Publish([FromBody] PublishLegalDocumentHttpRequest request, CancellationToken ct) =>
        (await service.PublishAsync(request.ToModel(), ct)).ToCreatedResult(this, nameof(Find), value => new { id = value.Id });
}
