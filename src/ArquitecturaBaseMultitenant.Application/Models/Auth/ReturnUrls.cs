using System.Diagnostics.CodeAnalysis;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Catálogo de destinos del front y validación de retornos permitidos para cada flujo de ingreso.</summary>
public static class ReturnUrls
{
    public const string AuthorizePath = "/connect/authorize";
    public const string LoginPath = "/login";
    public const string InvitationPath = "/invitacion";
    public const string LoginLinkPath = "/login/enlace";
    public const string ConsumerHomePath = "/";
    public const string BusinessHomePath = "/org";
    public const string AccountPath = "/cuenta";
    public const string BusinessLoginPath = "/login/empresa";
    public const string SignupPath = "/registro";
    public const string BusinessSignupPath = "/registro/empresa";
    public const string PlatformPath = "/plataforma";
    public const string CallbackPath = "/auth/callback";
    public const string SilentRenewPath = "/silent-renew.html";

    public static bool IsLocalAppPath([NotNullWhen(true)] string? path) =>
        !string.IsNullOrEmpty(path) && path[0] == '/'
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.Contains('\\') && !path.Any(char.IsControl);

    /// <summary>Sólo OTP: los retornos fijos exigen el acceso literal correspondiente; los anteriores no lo reciben.</summary>
    public static bool TryReadLoginCodeSelection(string? returnUrl, string? access,
        out AccessSelectionRequest? selection)
    {
        if (TryReadFixedLoginCodeSelection(returnUrl, out selection))
            return access == (selection.Access == Access.Consumer ? "consumer" : "business");
        if (access is not null) return false;
        return returnUrl == InvitationPath || TryReadAccessSelection(returnUrl, out selection);
    }

    /// <summary>El lado de cada retorno fijo es único; sólo se usa tras validar OTP o su ticket de cancelación.</summary>
    public static bool TryReadFixedLoginCodeSelection(string? returnUrl,
        [NotNullWhen(true)] out AccessSelectionRequest? selection)
    {
        selection = returnUrl switch
        {
            ConsumerHomePath => new AccessSelectionRequest(Access.Consumer, null),
            BusinessHomePath => new AccessSelectionRequest(Access.Business, null),
            _ => null,
        };
        return selection is not null;
    }

    /// <summary>La cancelación admite también los dos retornos fijos emitidos exclusivamente por OTP.</summary>
    public static bool IsLoginCodeDeletionReturn([NotNullWhen(true)] string? returnUrl) =>
        IsDeletionReturn(returnUrl) || TryReadFixedLoginCodeSelection(returnUrl, out _);

    /// <summary>La baja conserva los retornos probados y admite la continuación fija de un enlace personal.</summary>
    public static bool IsDeletionReturn([NotNullWhen(true)] string? returnUrl) =>
        returnUrl == LoginLinkPath || IsLoginReturn(returnUrl);

    public static bool IsLoginReturn([NotNullWhen(true)] string? returnUrl) =>
        returnUrl == InvitationPath || TryReadAccessSelection(returnUrl, out _);

    public static bool IsAuthorizeRequest([NotNullWhen(true)] string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith(AuthorizePath, StringComparison.Ordinal)
        && (returnUrl.Length == AuthorizePath.Length || returnUrl[AuthorizePath.Length] == '?')
        && !returnUrl.Any(char.IsControl);

    public static bool TryReadAccessSelection(string? returnUrl, [NotNullWhen(true)] out AccessSelectionRequest? selection)
    {
        selection = null;
        if (!IsAuthorizeRequest(returnUrl)) return false;

        string? accessText = null;
        string? tenantText = null;
        var queryStart = returnUrl.IndexOf('?');
        if (queryStart >= 0)
        {
            foreach (var parameter in returnUrl[(queryStart + 1)..].Split('&'))
            {
                var separator = parameter.IndexOf('=');
                var key = Decode(separator < 0 ? parameter : parameter[..separator]);
                var value = Decode(separator < 0 ? string.Empty : parameter[(separator + 1)..]);
                if (key == "access")
                {
                    if (accessText is not null) return false;
                    accessText = value;
                }
                else if (key == "tenant")
                {
                    if (tenantText is not null) return false;
                    tenantText = value;
                }
            }
        }

        var access = accessText switch
        {
            null or "consumer" => Access.Consumer,
            "business" => Access.Business,
            "platform" => Access.Platform,
            _ => (Access?)null,
        };
        if (access is null || tenantText is not null && !Guid.TryParse(tenantText, out _) ||
            access == Access.Platform && tenantText is not null)
        {
            return false;
        }

        selection = new AccessSelectionRequest(access.Value,
            tenantText is null ? null : Guid.Parse(tenantText));
        return true;
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal));
}
