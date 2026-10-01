namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>
/// Devuelve el método al que se envió un código y cuánto hay que esperar para reenviarlo. Permite
/// identificar el destino y controlar el botón de reenvío.
/// </summary>
public sealed record LoginMethodCodeResponse(Guid MethodId, int ResendAfterSeconds);
