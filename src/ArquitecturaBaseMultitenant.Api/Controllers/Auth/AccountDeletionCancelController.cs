using ArquitecturaBaseMultitenant.Api.Authentication;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth/deletion")]
[Tags("Auth")]
[OwnProtocol]
public sealed class AccountDeletionCancelController(PendingDeletionCookie pendingCookie) : ControllerBase
{
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
