using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

[ApiController]
[Route("api/me/external/google")]
[Access(Access.Consumer, Access.Business, Access.Platform)]
[OwnProtocol]
[Tags("Account")]
public sealed class AccountGoogleController(ICurrentUser currentUser, IAuthenticationSchemeProvider schemes,
    IAntiforgery antiforgery) : ControllerBase
{
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType<GoogleChallengeResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkGoogle()
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
            return Result.Failure(new ValidationError(new Dictionary<string, string[]>
                { ["requestVerificationToken"] = [ValidationTexts.Required] })).ToActionResult(this);
        if (await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null) return NotFound();
        if (currentUser.UserId is not { } userId) return Result.Failure(UserErrors.NotFound).ToActionResult(this);
        var properties = new AuthenticationProperties { RedirectUri = GoogleAccountLinkState.CallbackPath };
        properties.Items["LoginProvider"] = GoogleDefaults.AuthenticationScheme;
        properties.Items[GoogleAccountLinkState.UserIdKey] = userId.ToString("D");
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }
}
