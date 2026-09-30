using System.Globalization;
using System.Security.Claims;
using ArquitecturaBaseMultitenant.Api.Authentication;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>Passthrough MVC de autorización, token y userinfo de OpenIddict.</summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[OwnProtocol]
[Route("connect")]
public sealed class ConnectController(IConnectService service, IConnectLogoutService logoutService,
    OpenIdPrincipalFactory principalFactory) : ControllerBase
{
    [HttpGet("authorize")]
    [HttpPost("authorize")]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
        if (!TryReadAccess(request, out var access, out var requestedTenantId))
        {
            return Forbid(ErrorProperties(Errors.InvalidRequest, "The access selection is invalid."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var forceLogin = request.HasPromptValue(PromptValues.Login);
        var session = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!forceLogin && session.Succeeded && Guid.TryParse(
            session.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var userId))
        {
            var selected = await service.SelectAccessAsync(userId, access, requestedTenantId, cancellationToken);
            if (selected.IsFailure)
            {
                return Forbid(ErrorProperties(Errors.AccessDenied, AccessErrorDescription(selected.Error)),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var principal = await principalFactory.CreateAsync(userId, access, selected.Value.TenantId,
                request.GetScopes(), cancellationToken);
            if (principal is not null)
            {
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
        }

        if (request.HasPromptValue(PromptValues.None))
        {
            return Forbid(ErrorProperties(Errors.LoginRequired, "The user is not signed in."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // El SPA reanuda la autorización por GET también cuando el pedido llegó por formulario.
        var parameters = Request.HasFormContentType
            ? (await Request.ReadFormAsync(cancellationToken)).ToList()
            : Request.Query.ToList();
        // El código verificado reanuda este authorize con una cookie nueva.
        // Si se conserva prompt=login, volvería al formulario en un bucle.
        if (forceLogin)
        {
            parameters = parameters.Where(parameter => parameter.Key != Parameters.Prompt).ToList();
        }
        var loginPath = access == Access.Business ? "/login/empresa" : ReturnUrls.LoginPath;
        return Redirect(loginPath + QueryString.Create("returnUrl",
            ReturnUrls.AuthorizePath + QueryString.Create(parameters)));
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new InvalidOperationException("The grant type is not supported.");
        }

        var stored = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = stored.Principal is null
            ? null : await principalFactory.RefreshAsync(stored.Principal, cancellationToken);
        return principal is null
            ? Forbid(ErrorProperties(Errors.InvalidGrant, "The user can no longer sign in."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            : SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var hint = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var authorizationId = hint.Principal?.GetAuthorizationId();
        if (!string.IsNullOrEmpty(authorizationId))
        {
            await logoutService.RevokeAuthorizationAsync(authorizationId, cancellationToken);
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("userinfo")]
    [HttpPost("userinfo")]
    public async Task<IActionResult> UserInfo(CancellationToken cancellationToken)
    {
        var principal = (await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;
        var subject = principal?.GetClaim(Claims.Subject);
        var accessText = principal?.GetClaim(TenantClaimTypes.Access);
        var tenantText = principal?.GetClaim(TenantClaimTypes.TenantId);
        if (!Guid.TryParse(subject, CultureInfo.InvariantCulture, out var userId) ||
            !Enum.TryParse<Access>(accessText, true, out var access) || !Enum.IsDefined(access) ||
            (tenantText is not null && !Guid.TryParse(tenantText, CultureInfo.InvariantCulture, out _)))
        {
            return Challenge(ErrorProperties(Errors.InvalidToken, "The user can no longer sign in."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        Guid? tenantId = tenantText is null ? null : Guid.Parse(tenantText, CultureInfo.InvariantCulture);
        var user = await service.GetActiveUserAsync(userId, access, tenantId, cancellationToken);
        if (user.IsFailure)
        {
            return Challenge(ErrorProperties(Errors.InvalidToken, "The user can no longer sign in."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var account = user.Value.Account;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = account.Id.ToString("D", CultureInfo.InvariantCulture),
        };
        if (principal!.HasScope(Scopes.Email) && account.PrimaryEmail is { } email)
        {
            claims[Claims.Email] = email.Value;
            claims[Claims.EmailVerified] = true;
        }
        if (principal!.HasScope(Scopes.Profile))
        {
            if ((account.DisplayName ?? account.PrimaryEmail?.Value) is { } name)
            {
                claims[Claims.Name] = name;
            }
            claims[Claims.Locale] = account.Culture;
            claims[Claims.Zoneinfo] = account.TimeZoneId;
        }
        return Ok(claims);
    }

    private static bool TryReadAccess(OpenIddictRequest request, out Access access, out Guid? tenantId)
    {
        var value = request.GetParameter(TenantClaimTypes.Access).ToString();
        if (string.IsNullOrEmpty(value)) value = "consumer";
        if (!Enum.TryParse(value, true, out access) || !Enum.IsDefined(access))
        {
            tenantId = null;
            return false;
        }

        var tenant = request.GetParameter("tenant").ToString();
        tenantId = null;
        if (!string.IsNullOrEmpty(tenant))
        {
            if (!Guid.TryParse(tenant, CultureInfo.InvariantCulture, out var parsed)) return false;
            tenantId = parsed;
        }
        return access != Access.Platform || tenantId is null;
    }

    private static AuthenticationProperties ErrorProperties(string error, string description) =>
        new(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        });

    private static string AccessErrorDescription(Error error)
    {
        // El callback necesita el nombre aprobado del estado de membresía; no exponemos otras metadata.
        if (error.Metadata is null ||
            !error.Metadata.TryGetValue("organizationName", out var value) || value is not string name)
        {
            return error.Code;
        }

        return error.Code + "|" + Uri.EscapeDataString(name);
    }
}
