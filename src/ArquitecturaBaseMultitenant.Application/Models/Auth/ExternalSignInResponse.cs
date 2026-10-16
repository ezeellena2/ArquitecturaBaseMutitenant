namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>
/// Devuelve el destino al que continúa el navegador después de completar el ingreso externo. Permite que el
/// controller finalice el flujo con la redirección indicada por el servicio. Con <see cref="PlatformFactorPending"/> (E9) el
/// operador todavía no tiene sesión: el navegador vuelve al ingreso para el segundo factor.
/// </summary>
public sealed record ExternalSignInResponse(string ReturnUrl, bool PlatformFactorPending = false);
