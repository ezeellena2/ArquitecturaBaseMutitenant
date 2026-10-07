namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>La espera de un pedido de código aceptado, compartida por ingreso y registro.</summary>
public sealed record RequestLoginCodeResponse(int ResendAfterSeconds);
