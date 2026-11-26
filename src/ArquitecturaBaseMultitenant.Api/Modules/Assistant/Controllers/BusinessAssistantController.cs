using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[Route("api/organization/assistant")]
[Tags("Assistant Business")]
[Access(Access.Business)]
[FeatureGate("assistant")]
[HasPermission(Permissions.Assistant.Use)]
public sealed class BusinessAssistantController(IAssistantConversationService service, IAssistantAttachmentService attachments, IAssistantExportService exports,
    IAssistantProposalService proposals) : AssistantControllerBase(service, attachments, exports)
{
    /// <summary>La propuesta de quien pide, con su estado calculado al leer (ADR 0068 §9). 501 hasta la B5.</summary>
    [HttpGet("proposals/{proposalId:guid}")]
    [ProducesResponseType<AssistantProposalResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> GetProposal([FromRoute] Guid proposalId, CancellationToken cancellationToken) =>
        (await proposals.GetAsync(proposalId, cancellationToken)).ToActionResult(this);

    /// <summary>
    /// La clave del intento de guardar (ADR 0068 §10). Sin <c>[Idempotent]</c>: es idempotente por naturaleza, porque repetirlo
    /// devuelve la misma clave mientras ese intento no termine en 4xx, y responde 200 sin crear nada. 501 hasta la B5.
    /// </summary>
    [HttpPost("proposals/{proposalId:guid}/attempts")]
    [ProducesResponseType<AssistantProposalAttemptResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> AttemptProposal([FromRoute] Guid proposalId, [FromBody] AssistantProposalAttemptHttpRequest request,
        CancellationToken cancellationToken) =>
        (await proposals.AttemptAsync(proposalId, cancellationToken)).ToActionResult(this);

    /// <summary>
    /// La pantalla guardó: el servidor lo comprueba y la marca aplicada (ADR 0068 §10). Al editar, la versión del cuerpo es la
    /// nueva del registro, la que devuelve <c>target.version</c>. 501 hasta la B5.
    /// </summary>
    [HttpPost("proposals/{proposalId:guid}/applied")]
    [ResponseVersion("Target.Version")]
    [ProducesResponseType<AssistantProposalResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> AppliedProposal([FromRoute] Guid proposalId, [FromBody] AssistantProposalAppliedHttpRequest request,
        CancellationToken cancellationToken) =>
        (await proposals.AppliedAsync(proposalId, new AssistantProposalAppliedRequest(request.Version), cancellationToken)).ToActionResult(this);
}
