namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// El paso de un desafío de segundo factor (E9): probar el autenticador ya configurado, darlo de alta o mostrar los
/// ocho códigos del alta hasta que el operador confirme que los guardó.
/// </summary>
public enum PlatformFactorStep
{
    Verify,
    Setup,
    RecoveryCodes,
}
