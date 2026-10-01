namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Identifica si un método de ingreso usa correo, teléfono o Google. El tipo determina cómo se interpreta
/// su valor y qué canal puede verificarlo.
/// </summary>
public enum LoginMethodType
{
    Email,
    Phone,
    Google,
}
