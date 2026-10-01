namespace ArquitecturaBaseMultitenant.Domain.Users;

/// <summary>
/// Identifica con qué acceso opera la sesión: como persona, como empresa o como operador de plataforma. Las
/// rutas y las reglas lo usan para separar las operaciones permitidas.
/// </summary>
public enum Access
{
    Consumer,
    Business,
    Platform,
}
