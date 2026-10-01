using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace ArquitecturaBaseMultitenant.Api.RequestContext;

/// <summary>Continúa el flujo tras ingreso/F5 sin exponer el token a almacenamiento JS, URL HTTP ni estado OIDC.</summary>
public sealed class InvitationFlowContext(IHttpContextAccessor accessor, IDataProtectionProvider protection,
    IUserStatusReader users, ISignInService signIn, TimeProvider timeProvider) : IInvitationFlowContext
{
    private const string CookieName = "__Host-MtInvitation";
    private readonly IDataProtector _protector = protection.CreateProtector("Invitation.Continuation.v1");
    private HttpContext Context => accessor.HttpContext ?? throw new InvalidOperationException("An HTTP request is required.");
    private static CookieOptions Options => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/", IsEssential = true,
    };

    public async Task<Guid?> GetUserIdAsync(CancellationToken cancellationToken)
    {
        var principal = Context.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            var cookie = await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            principal = cookie.Succeeded ? cookie.Principal! : new ClaimsPrincipal();
        }
        var claim = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, CultureInfo.InvariantCulture, out var userId) && userId != Guid.Empty
            && await users.GetStatusAsync(userId, cancellationToken) == UserStatus.Active ? userId : null;
    }

    public InvitationContinuation? ReadContinuation()
    {
        if (!Context.Request.Cookies.TryGetValue(CookieName, out var value) || value.Length > 8192) return null;
        try
        {
            var state = JsonSerializer.Deserialize<InvitationContinuation>(_protector.Unprotect(value));
            return state is not null && state.ExpiresAtUtc.Kind == DateTimeKind.Utc
                && state.ExpiresAtUtc > timeProvider.GetUtcNow().UtcDateTime
                && state.Token is { Length: > 0 and <= 4096 }
                && state.Nonce is { } nonce && ISecureTokenGenerator.HasTokenFormat(nonce) ? state : null;
        }
        catch (CryptographicException) { return null; }
        catch (JsonException) { return null; }
    }

    public void Remember(InvitationContinuation continuation)
    {
        var options = Options;
        options.Expires = new DateTimeOffset(continuation.ExpiresAtUtc);
        Context.Response.Cookies.Append(CookieName, _protector.Protect(JsonSerializer.Serialize(continuation)), options);
        Context.Response.Headers.CacheControl = "no-store";
    }

    public void ClearContinuation() => Context.Response.Cookies.Delete(CookieName, Options);

    public async Task<bool> RestoreSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await users.GetStatusAsync(userId, cancellationToken) != UserStatus.Active
            || await signIn.IsLockedOutAsync(userId, cancellationToken)) return false;
        await signIn.SignInAsync(userId, cancellationToken);
        return true;
    }
}
