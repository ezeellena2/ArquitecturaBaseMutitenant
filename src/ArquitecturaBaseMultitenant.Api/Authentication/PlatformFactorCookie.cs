using System.Globalization;
using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace ArquitecturaBaseMultitenant.Api.Authentication;

/// <summary>
/// La cookie protegida del desafío pendiente de Plataforma (E9): HttpOnly, Secure y host-only (prefijo __Host-),
/// SameSite=Strict y de sesión del navegador: el vencimiento lo controla el desafío en el servidor, con su reloj. No es
/// un esquema de autenticación: sólo la lee el segundo factor, en el host principal y, en una escritura, con el origen
/// público.
/// </summary>
public sealed class PlatformFactorCookie(IHttpContextAccessor accessor, IDataProtectionProvider protection,
    IPublicOrigin publicOrigin) : IPlatformFactorBrowser
{
    internal const string CookieName = "__Host-MtPlatformFactor";
    private const char Separator = '.';
    private readonly IDataProtector _protector = protection.CreateProtector("ArquitecturaBaseMultitenant.PlatformFactor.Binding.v1");

    public PlatformFactorBinding? Read()
    {
        if (accessor.HttpContext is not { } http || !IsMainHost(http.Request)
            || !HttpMethods.IsGet(http.Request.Method) && !HasPublicOrigin(http.Request)
            || !http.Request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value))
            return null;
        string payload;
        try
        {
            payload = _protector.Unprotect(value);
        }
        catch (CryptographicException)
        {
            return null;
        }
        var separator = payload.IndexOf(Separator, StringComparison.Ordinal);
        return separator > 0 && Guid.TryParseExact(payload[..separator], "N", out var challengeId)
            && payload[(separator + 1)..] is { Length: > 0 } token
            ? new PlatformFactorBinding(challengeId, token)
            : null;
    }

    public void Write(PlatformFactorBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var http = accessor.HttpContext ?? throw new InvalidOperationException("The challenge cookie needs a request.");
        var payload = binding.ChallengeId.ToString("N", CultureInfo.InvariantCulture) + Separator + binding.Token;
        http.Response.Cookies.Append(CookieName, _protector.Protect(payload), Options());
    }

    public void Clear() => accessor.HttpContext?.Response.Cookies.Delete(CookieName, Options());

    private static CookieOptions Options() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
    };

    // Un subdominio de organización nunca participa del ingreso de Plataforma.
    private bool IsMainHost(HttpRequest request) => publicOrigin.Value is not { } origin
        || !request.Host.Host.EndsWith("." + origin.Host, StringComparison.OrdinalIgnoreCase);

    private bool HasPublicOrigin(HttpRequest request) => publicOrigin.Value is not { } origin
        || Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var sent)
            && Uri.Compare(sent, origin, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
}
