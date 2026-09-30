using ArquitecturaBaseMultitenant.Api.Contracts.Account;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

/// <summary>Solicita la baja de la cuenta actual con un comprobante de reautenticación y cierra su cookie al aceptarla.</summary>
[ApiController]
[Route("api/me/deletion")]
[Access(Access.Consumer, Access.Business, Access.Platform)]
[Tags("Account")]
public sealed class AccountDeletionController(IAccountDeletionService service) : ControllerBase
{
    [HttpPost]
    [Idempotent]
    [ProducesResponseType<AccountDeletionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestDeletion([FromBody] RequestAccountDeletionHttpRequest request, CancellationToken ct)
    {
        var result = await service.RequestAsync(new RequestAccountDeletionRequest(request.Reason, request.ReauthTicket), ct);
        if (result.IsSuccess) await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return result.ToActionResult(this);
    }
}
