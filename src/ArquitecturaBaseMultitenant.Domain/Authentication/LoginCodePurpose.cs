namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Para qué se pidió un código. Solo sirve para el propósito con el que se emitió.
/// </summary>
public enum LoginCodePurpose
{
    /// <summary>Entrar con un método verificado.</summary>
    Login = 1,

    /// <summary>Crear una cuenta personal.</summary>
    Signup = 2,

    /// <summary>
    /// Demostrar, desde el perfil, que un número o un correo es de quien lo quiere vincular. Solo sirve para la cuenta
    /// que lo pidió.
    /// </summary>
    VerifyDestination = 3,

    /// <summary>Confirmar que quien cambia un método sigue controlando otro método.</summary>
    Reauthenticate = 4,
}
