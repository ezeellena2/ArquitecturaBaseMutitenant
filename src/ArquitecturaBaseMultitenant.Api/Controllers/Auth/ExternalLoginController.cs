using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>Desafío Google con contexto de Registro o Ingreso protegido por OAuth state.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth/external")]
[Tags("Auth")]
[OwnProtocol]
public sealed class ExternalLoginController(
    IExternalLoginService service,
    IAuthenticationSchemeProvider schemes) : ControllerBase
{
    private const string CallbackPath = "/api/auth/external/callback";
    private const string SignupKey = "Signup";
    private const string AcceptedTermsKey = "AcceptedTerms";
    private const string ReturnUrlKey = "ReturnUrl";
    private const string ReturnToKey = "ReturnTo";
    private const string AccessKey = "Access";
    private const string CultureKey = "Culture";
    private const string TimeZoneKey = "TimeZoneId";
    private const string LoginProviderKey = "LoginProvider";

    [HttpGet("google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Google([FromQuery] ExternalLoginQuery query)
    {
        if (query.Signup) return Invalid("signup", ValidationTexts.Required);
        return await StartGoogleChallengeAsync(query);
    }

    [HttpGet("google/antiforgery")]
    [ProducesResponseType<GoogleSignupAntiforgeryResponse>(StatusCodes.Status200OK)]
    public IActionResult GoogleSignupAntiforgery([FromServices] IAntiforgery antiforgery)
    {
        Response.Headers.CacheControl = "no-store";
        var token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken
            ?? throw new InvalidOperationException("The antiforgery request token was not created.");
        return Ok(new GoogleSignupAntiforgeryResponse(token));
    }

    [HttpPost("google")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GoogleSignup(
        [FromForm] ExternalLoginQuery query, [FromServices] IAntiforgery antiforgery)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
            return Invalid("requestVerificationToken", ValidationTexts.Required);
        if (!query.Signup) return Invalid("signup", ValidationTexts.Required);
        return await StartGoogleChallengeAsync(query);
    }

    private async Task<IActionResult> StartGoogleChallengeAsync(ExternalLoginQuery query)
    {
        if (await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
        {
            return NotFound();
        }

        if (query.Signup)
        {
            if (!query.AcceptedTerms) return Invalid("acceptedTerms", ValidationTexts.Required);
            if (!IsLocalAppPath(query.ReturnTo)) return Invalid("returnTo", ValidationTexts.ReturnUrlInvalid);
        }
        else if (!ReturnUrls.IsAuthorizeRequest(query.ReturnUrl))
        {
            return Invalid("returnUrl", ValidationTexts.ReturnUrlInvalid);
        }

        var properties = new AuthenticationProperties { RedirectUri = CallbackPath };
        properties.Items[LoginProviderKey] = GoogleDefaults.AuthenticationScheme;
        properties.Items[SignupKey] = query.Signup ? "true" : "false";
        properties.Items[AcceptedTermsKey] = query.AcceptedTerms ? "true" : "false";
        properties.Items[ReturnUrlKey] = query.Signup ? ReturnUrls.AuthorizePath : query.ReturnUrl;
        properties.Items[ReturnToKey] = query.Signup ? query.ReturnTo : null;
        properties.Items[AccessKey] = query.Access == "business" ? "business" : "consumer";
        properties.Items[CultureKey] = query.Culture;
        properties.Items[TimeZoneKey] = query.TimeZoneId;
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Callback(CancellationToken cancellationToken)
    {
        var authenticated = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        var state = authenticated.Properties?.Items;
        if (!authenticated.Succeeded || state is null)
        {
            return RedirectWithError(ReturnUrls.LoginPath, ExternalLoginErrors.FailedCode);
        }

        var signup = Value(state, SignupKey) == "true";
        var request = new ExternalSignInRequest(Value(state, ReturnUrlKey), signup,
            Value(state, AcceptedTermsKey) == "true", Value(state, CultureKey), Value(state, TimeZoneKey));
        var result = await service.SignInAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            var destination = signup && result.Error.Code == "Auth.Signup.Closed"
                ? "/registro"
                : Value(state, AccessKey) == "business" ? "/login/empresa" : ReturnUrls.LoginPath;
            return RedirectWithError(destination, result.Error.Code);
        }

        if (!signup) return LocalRedirect(result.Value.ReturnUrl);
        var returnTo = Value(state, ReturnToKey);
        if (!IsLocalAppPath(returnTo)) return LocalRedirect("/registro?google=complete");
        return LocalRedirect("/registro" + QueryString.Create(
            new Dictionary<string, string?> { ["google"] = "complete", ["returnTo"] = returnTo }));
    }

    private LocalRedirectResult RedirectWithError(string path, string code) =>
        LocalRedirect(path + QueryString.Create("error", code));

    private IActionResult Invalid(string field, string message) =>
        Result.Failure(new ValidationError(new Dictionary<string, string[]> { [field] = [message] }))
            .ToActionResult(this);

    private static string? Value(IDictionary<string, string?> state, string key) =>
        state.TryGetValue(key, out var value) ? value : null;

    private static bool IsLocalAppPath(string? path) =>
        !string.IsNullOrEmpty(path)
        && path[0] == '/' && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.Contains('\\') && !path.Any(char.IsControl);
}
