using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[ApiController]
[Route("api/platform/assistant")]
[Tags("Assistant Platform")]
[Access(Access.Platform)]
[InstalledModuleRoute(AssistantApiModule.ChatRoute, InstalledModuleRouteCategory.PlatformChat)]
[HasPlatformPermission(PlatformPermissions.Assistant.Use)]
public sealed class PlatformAssistantController(IAssistantConversationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AssistantStateResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> State(CancellationToken cancellationToken) =>
        (await service.StateAsync(cancellationToken)).ToActionResult(this);

    [HttpPost("notice")]
    [Idempotent(Mode = IdempotencyMode.BufferedJson)]
    [IdempotencyReplay(typeof(AssistantNoticeReplayVerifier))]
    [ProducesResponseType(204)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> AcceptNotice([FromBody] AssistantNoticeHttpRequest request, CancellationToken cancellationToken) =>
        (await service.AcceptNoticeAsync(new AssistantNoticeRequest(request.NoticeVersion), cancellationToken)).ToActionResult(this);

    [HttpGet("conversations")]
    [ProducesResponseType<CursorResult<AssistantConversationSummaryResponse>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> List([FromQuery] AssistantConversationListHttpQuery request, CancellationToken cancellationToken) =>
        (await service.ListAsync(new AssistantConversationListRequest(request.Cursor, request.Limit, request.Saved), cancellationToken)).ToActionResult(this);

    [HttpPost("conversations")]
    [Idempotent(Mode = IdempotencyMode.DurableReference)]
    [ProducesResponseType<AssistantConversationSummaryResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Create([FromBody] CreateAssistantConversationHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CreateAsync(new CreateAssistantConversationRequest(), cancellationToken)).ToCreatedResult(this, nameof(Get), value => new { id = value.Id });

    [HttpGet("conversations/{id:guid}")]
    [ProducesResponseType<AssistantConversationResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Get([FromRoute] Guid id, [FromQuery] AssistantMessageListHttpQuery request, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, new AssistantMessageListRequest(request.Cursor, request.Limit), cancellationToken)).ToActionResult(this);

    [HttpPut("conversations/{id:guid}")]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateAssistantConversationHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(id, new UpdateAssistantConversationRequest(request.Title, request.IsSaved, request.Version), cancellationToken)).ToActionResult(this);

    [HttpDelete("conversations/{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, [FromQuery] AssistantDeleteHttpQuery request, CancellationToken cancellationToken) =>
        (await service.DeleteAsync(id, request.Version, cancellationToken)).ToActionResult(this);

    [HttpPost("conversations/{id:guid}/messages")]
    [Idempotent(Mode = IdempotencyMode.DurableStream)]
    [ProducesResponseType<AssistantTurnResultResponse>(200)]
    [ProducesResponseType<AssistantStreamHttpResponse>(200, "text/event-stream")]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(413)]
    [ProducesProblem(422)]
    [ProducesProblem(429)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    [ProducesProblem(503)]
    public async Task<IActionResult> Send([FromRoute] Guid id, [FromBody] SendAssistantMessageHttpRequest request, CancellationToken cancellationToken) =>
        (await service.SendAsync(id, new SendAssistantMessageRequest(request.Text, request.AttachmentIds, request.ConversationVersion), cancellationToken)).ToActionResult(this);

    [HttpPost("conversations/{id:guid}/turns/{turnId:guid}/stop")]
    [Idempotent(Mode = IdempotencyMode.BufferedJson)]
    [ProducesResponseType(204)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Stop([FromRoute] Guid id, [FromRoute] Guid turnId, [FromBody] StopAssistantTurnHttpRequest request, CancellationToken cancellationToken) =>
        (await service.StopAsync(id, turnId, new StopAssistantTurnRequest(), cancellationToken)).ToActionResult(this);

    [HttpPost("quick/{key}")]
    [Idempotent(Mode = IdempotencyMode.DurableReference)]
    [ResponseVersion("Conversation.Version")]
    [ProducesResponseType<AssistantQuickResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Quick([FromRoute] string key, [FromBody] AssistantQuickHttpRequest request, CancellationToken cancellationToken) =>
        (await service.QuickAsync(key, new AssistantQuickRequest(request.ConversationId, request.Version), cancellationToken)).ToActionResult(this);

    [HttpPut("conversations/{id:guid}/messages/{messageId:guid}/feedback")]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Feedback([FromRoute] Guid id, [FromRoute] Guid messageId, [FromBody] AssistantFeedbackHttpRequest request, CancellationToken cancellationToken) =>
        (await service.FeedbackAsync(id, messageId, new AssistantFeedbackRequest(request.Rating, request.Version), cancellationToken)).ToActionResult(this);

    [HttpPost("conversations/{id:guid}/attachments")]
    [Idempotent(Mode = IdempotencyMode.DurableReference)]
    [ProducesResponseType<AssistantAttachmentSummaryResponse>(201)]
    [Consumes("multipart/form-data")]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(413)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Upload([FromRoute] Guid id, [FromForm] AssistantUploadHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UploadAsync(id, new AssistantUploadRequest(request.File?.FileName, request.File?.ContentType, request.File?.Length, null, request.ConversationVersion), cancellationToken)).ToCreatedResult(this, nameof(Download), value => new { id, attachmentId = value.Id });

    [HttpGet("conversations/{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType<byte[]>(200, "application/octet-stream")]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(413)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Download([FromRoute] Guid id, [FromRoute] Guid attachmentId, CancellationToken cancellationToken) =>
        (await service.DownloadAsync(id, attachmentId, cancellationToken)).ToActionResult(this);

    [HttpDelete("conversations/{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(204)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(413)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> DeleteAttachment([FromRoute] Guid id, [FromRoute] Guid attachmentId, [FromQuery] AssistantDeleteHttpQuery request, CancellationToken cancellationToken) =>
        (await service.DeleteAttachmentAsync(id, attachmentId, request.Version, cancellationToken)).ToActionResult(this);

    [HttpGet("conversations/{id:guid}/export")]
    [ProducesResponseType<byte[]>(200, "application/octet-stream")]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public async Task<IActionResult> Export([FromRoute] Guid id, [FromQuery] AssistantExportHttpQuery request, CancellationToken cancellationToken) =>
        (await service.ExportAsync(id, request.Format, cancellationToken)).ToActionResult(this);
}
