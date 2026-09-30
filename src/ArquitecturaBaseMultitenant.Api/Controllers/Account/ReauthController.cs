using ArquitecturaBaseMultitenant.Api.Contracts.Account;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

[ApiController]
[Route("api/me/reauth")]
[Access(Access.Consumer, Access.Business, Access.Platform)]
[Tags("Account")]
public sealed class ReauthController(IReauthService service) : ControllerBase
{
    [HttpPost]
    [Idempotent]
    [ProducesResponseType<ReauthCodeResponse>(StatusCodes.Status202Accepted)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestCode([FromBody] RequestReauthHttpRequest request, CancellationToken ct) =>
        (await service.RequestAsync(new RequestReauthRequest(request.Action ?? (ReauthAction)(-1), request.TargetMethodId), ct))
            .ToAcceptedResult(this);

    [HttpPost("verify")]
    [Idempotent]
    [ProducesResponseType<ReauthResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Verify([FromBody] VerifyReauthHttpRequest request, CancellationToken ct) =>
        (await service.VerifyAsync(new VerifyReauthRequest(request.Action ?? (ReauthAction)(-1), request.TargetMethodId,
            request.SourceMethodId ?? Guid.Empty, request.Code), ct)).ToActionResult(this);
}
