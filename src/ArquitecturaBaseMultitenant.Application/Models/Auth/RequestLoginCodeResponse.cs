namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>La misma forma exista o no la cuenta: no revela si el correo está registrado.</summary>
public sealed record RequestLoginCodeResponse(int ResendAfterSeconds);
