using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Contracts.Account;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

/// <summary>Inicia el desafío de Google para vincularlo a la cuenta actual; la vinculación ocurre tras el callback validado.</summary>
[ApiController]
[Route("api/me/external/google")]
[Access(Access.Consumer, Access.Business, Access.Platform)]
[OwnProtocol]
[Tags("Account")]
public sealed class AccountGoogleController(IAccountGoogleService service, IAuthenticationSchemeProvider schemes,
    IAntiforgery antiforgery) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<GoogleChallengeResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkGoogle([FromBody] ChangeLoginMethodHttpRequest request, CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
            return Result.Failure(new ValidationError(new Dictionary<string, string[]>
                { ["requestVerificationToken"] = [ValidationTexts.Required] })).ToActionResult(this);
        var identity = await service.GetLinkUserIdAsync(request.ReauthTicket, cancellationToken);
        if (identity.IsFailure) return Result.Failure(identity.Error).ToActionResult(this);
        if (await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null) return NotFound();
        var properties = new AuthenticationProperties { RedirectUri = GoogleAccountLinkState.CallbackPath };
        properties.Items["LoginProvider"] = GoogleDefaults.AuthenticationScheme;
        properties.Items[GoogleAccountLinkState.UserIdKey] = identity.Value.ToString("D");
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }
}
