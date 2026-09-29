using System.Diagnostics.CodeAnalysis;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Solo se vuelve a la autorización local; una URL arbitraria sería una redirección abierta.</summary>
public static class ReturnUrls
{
    public const string AuthorizePath = "/connect/authorize";
    public const string LoginPath = "/login";

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
