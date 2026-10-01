namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// Indica si la plataforma admite nuevas cuentas personales. La regla de registro consulta este valor antes
/// de crear una cuenta.
/// </summary>
public enum ConsumerSignupMode
{
    Open,
    Closed,
}
