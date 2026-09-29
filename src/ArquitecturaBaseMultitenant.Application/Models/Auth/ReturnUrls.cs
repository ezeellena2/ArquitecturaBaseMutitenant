using System.Diagnostics.CodeAnalysis;

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
}
