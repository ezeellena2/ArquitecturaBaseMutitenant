using ArquitecturaBaseMultitenant.Api.Contracts.Account;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

[ApiController]
[Route("api/legal/accept")]
[Access(Access.Consumer, Access.Business, Access.Platform)]
[Tags("Legal")]
public sealed class LegalAcceptanceController(ILegalAcceptanceService service) : ControllerBase
{
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept([FromBody] AcceptLegalHttpRequest request, CancellationToken ct) =>
        (await service.AcceptAsync(new AcceptLegalRequest(request.Documents), ct)).ToActionResult(this);
}
