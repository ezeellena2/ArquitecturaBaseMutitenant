namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>
/// Devuelve el destino al que continúa el navegador después de completar el ingreso externo. Permite que el
/// controller finalice el flujo con la redirección indicada por el servicio.
/// </summary>
public sealed record ExternalSignInResponse(string ReturnUrl);
