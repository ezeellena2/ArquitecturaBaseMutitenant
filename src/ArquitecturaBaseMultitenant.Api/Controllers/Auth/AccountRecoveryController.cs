using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>
/// «Recuperar mi cuenta», sin sesión. Valida la cuenta anterior y el nuevo correo sin entregar una sesión;
/// los límites por IP son los del ingreso por código.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/account-recovery")]
[Tags("Auth")]
public sealed class AccountRecoveryController(IAccountRecoveryService service) : ControllerBase
{
    [HttpPost("preview")]
    [EnableRateLimiting(RateLimitPolicies.LoginCode)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Preview([FromBody] PreviewAccountRecoveryHttpRequest request,
        CancellationToken cancellationToken)
    {
        var email = AuthContactMapping.ParseEmail(request.PreviousEmail, request.PreviousPhone, null, "previousEmail", "previousPhone");
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.PreviewAsync(new PreviewAccountRecoveryRequest(email.Value), cancellationToken))
            .ToActionResult(this);
    }

    [HttpPost("code")]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.LoginCode)]
    [ProducesResponseType<RequestLoginCodeResponse>(StatusCodes.Status202Accepted)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestCode([FromBody] RequestRecoveryCodeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var email = AuthContactMapping.ParseEmail(request.Email, request.Phone, request.Channel);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.RequestCodeAsync(new RequestRecoveryCodeRequest(email.Value, request.Nonce),
            cancellationToken)).ToAcceptedResult(this);
    }

    [HttpPost("verify")]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.LoginVerify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyRecoveryCodeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var email = AuthContactMapping.ParseEmail(request.Email, request.Phone, request.Channel);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.VerifyCodeAsync(new VerifyRecoveryCodeRequest(email.Value, request.Nonce, request.Code),
            cancellationToken)).ToActionResult(this);
    }

    [HttpPost]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.LoginVerify)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] SubmitAccountRecoveryHttpRequest request,
        CancellationToken cancellationToken)
    {
        var email = AuthContactMapping.ParseEmail(request.Email, request.Phone, request.Channel);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        Email? previousEmail = null;
        if (!string.IsNullOrWhiteSpace(request.PreviousEmail))
        {
            var parsed = AuthEmailMapping.Parse(request.PreviousEmail, "previousEmail");
            if (parsed.IsFailure) return Result.Failure(parsed.Error).ToActionResult(this);
            previousEmail = parsed.Value;
        }

        return (await service.SubmitAsync(new SubmitAccountRecoveryRequest(email.Value, request.Nonce, previousEmail,
            request.PreviousPhone?.Country, request.PreviousPhone?.Number), cancellationToken)).ToAcceptedResult(this);
    }
}
