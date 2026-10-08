using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhooks/whatsapp")]
[InstalledModuleRoute(WhatsAppApiModule.WebhookRoute)]
[Tags("WhatsApp")]
public sealed class WhatsAppWebhookController(IWhatsAppWebhookService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/plain")]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Verify([FromQuery] WhatsAppWebhookQuery query, CancellationToken ct)
    {
        var result = await service.VerifyAsync(query.Mode, query.VerifyToken, query.Challenge, ct);
        return result.IsFailure ? result.ToActionResult(this) : Content(result.Value, "text/plain");
    }

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [EnableRateLimiting(WhatsAppApiModule.WebhookRateLimit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status413PayloadTooLarge)]
    [ProducesProblem(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Receive([FromBody] WhatsAppWebhookHttpRequest request,
        [FromHeader(Name = "X-Hub-Signature-256")] string? signature, CancellationToken ct)
    {
        var result = await service.ReceiveAsync(new WhatsAppWebhookRequest(request.ObjectKind, request.Entry, signature,
            HttpContext.Items[Filters.WhatsAppWebhookFilter.OriginalBodyKey] is byte[] bytes ? bytes : default), ct);
        // ACK de protocolo Meta: 200 sin cuerpo después del commit, en lugar del 204 de una escritura de negocio.
        return result.IsFailure ? result.ToActionResult(this) : new StatusCodeResult(StatusCodes.Status200OK);
    }
}
