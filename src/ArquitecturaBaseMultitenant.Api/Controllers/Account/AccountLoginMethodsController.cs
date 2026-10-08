using ArquitecturaBaseMultitenant.Api.Contracts.Account;
using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

/// <summary>Expone los métodos de la cuenta y delega su alta, verificación, elección principal y baja al servicio de identidad.</summary>
[ApiController]
[Route("api/me/login-methods")]
[Access(Access.Consumer, Access.Business, Access.Platform)]
[Tags("Account")]
public sealed class AccountLoginMethodsController(ILoginMethodManagementService service,
    IAccountLoginMethodsService listing) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AccountLoginMethodsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => (await listing.ListAsync(ct)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<LoginMethodCodeResponse>(StatusCodes.Status202Accepted)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Add([FromBody] AddLoginMethodHttpRequest request, CancellationToken ct)
    {
        var email = AuthContactMapping.ParseEmail(request.Email, request.Phone, request.Channel);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.AddAsync(new AddLoginMethodRequest(email.Value, Channel: request.Channel), ct)).ToAcceptedResult(this);
    }

    [HttpPost("{methodId:guid}/code")]
    [Idempotent]
    [ProducesResponseType<LoginMethodCodeResponse>(StatusCodes.Status202Accepted)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendCode(Guid methodId, CancellationToken ct) =>
        (await service.SendCodeAsync(methodId, ct)).ToAcceptedResult(this);

    [HttpPost("{methodId:guid}/verify")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verify(Guid methodId, [FromBody] VerifyLoginMethodHttpRequest request, CancellationToken ct) =>
        (await service.VerifyAsync(new VerifyLoginMethodRequest(methodId, request.Code), ct)).ToActionResult(this);

    [HttpPut("{methodId:guid}/primary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Primary(Guid methodId, CancellationToken ct) =>
        (await service.MakePrimaryAsync(new ChangeLoginMethodRequest(methodId), ct)).ToActionResult(this);

    [HttpDelete("{methodId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid methodId, CancellationToken ct) =>
        (await service.RemoveAsync(new ChangeLoginMethodRequest(methodId), ct)).ToActionResult(this);
}
