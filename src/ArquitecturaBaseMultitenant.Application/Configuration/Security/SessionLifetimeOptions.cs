using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Security;

/// <summary>
/// Las vidas de los tokens de OpenIddict y de la cookie del navegador (E9), en Authentication:Lifetimes, con los valores
/// de siempre: código de autorización 5 minutos, access token 15 minutos, refresh token y cookie 30 días.
/// </summary>
public sealed class SessionLifetimeOptions
{
    public const string SectionName = "Authentication:Lifetimes";

    [Range(1, 10)]
    public int AuthorizationCodeMinutes { get; init; } = 5;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 30;

    [Range(1, 90)]
    public int BrowserCookieDays { get; init; } = 30;
}
