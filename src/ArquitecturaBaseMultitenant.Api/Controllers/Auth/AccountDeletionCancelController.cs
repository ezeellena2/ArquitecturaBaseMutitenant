using ArquitecturaBaseMultitenant.Api.Authentication;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>Expone sin sesión el estado de baja pendiente desde una cookie protegida y permite cancelarla con un ticket.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth/deletion")]
[Tags("Auth")]
[OwnProtocol]
public sealed class AccountDeletionCancelController(PendingDeletionCookie pendingCookie, IAccountDeletionService service) : ControllerBase
{
    [HttpPost("cancel")]
    [Idempotent]
    [ProducesResponseType<CancelAccountDeletionHttpResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(CancelAccountDeletionHttpRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await service.CancelAsync(new CancelAccountDeletionRequest(request.CancelTicket), ct);
        if (result.IsFailure) return result.ToActionResult(this);
        pendingCookie.Clear(HttpContext);
        return Ok(new CancelAccountDeletionHttpResponse(result.Value.ReturnUrl));
    }

    [HttpPost("pending")]
    [ProducesResponseType<PendingDeletionState>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public IActionResult Pending()
    {
        Response.Headers.CacheControl = "no-store";
        var state = pendingCookie.Read(HttpContext);
        return state is null ? NotFound() : Ok(state);
    }
}
