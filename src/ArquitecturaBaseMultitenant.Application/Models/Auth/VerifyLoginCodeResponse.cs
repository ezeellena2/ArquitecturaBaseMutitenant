namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Authorize original que el SPA vuelve a abrir después de crear la sesión.</summary>
public sealed record VerifyLoginCodeResponse(string ReturnUrl);
