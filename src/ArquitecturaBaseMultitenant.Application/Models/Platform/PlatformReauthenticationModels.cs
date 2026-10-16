using ArquitecturaBaseMultitenant.Application.Models.Auth;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>«Confirmá que sos vos»: el código del autenticador o uno de recuperación; nunca se registra.</summary>
public sealed record PlatformReauthenticationRequest(TwoFactorMethod? Method, string? Code)
{
    public override string ToString() => nameof(PlatformReauthenticationRequest);
}

/// <summary>
/// La prueba reciente quedó ligada a esta sesión de Plataforma hasta <see cref="RecentUntilUtc"/> (10 minutos, P1). Un
/// replay de la misma operación conserva el vencimiento original.
/// </summary>
public sealed record PlatformReauthenticationResponse(DateTime VerifiedAtUtc, DateTime RecentUntilUtc)
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
}
