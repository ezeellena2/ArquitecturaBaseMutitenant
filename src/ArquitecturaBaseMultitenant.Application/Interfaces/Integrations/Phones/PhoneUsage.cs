namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;

/// <summary>
/// Qué tipo de número acepta un campo. <see cref="Any"/>: fijo o celular (datos de contacto); <see cref="Mobile"/>:
/// solo celular (ingreso, registro y teléfono de la cuenta). No nombra WhatsApp: sus países los controla el módulo.
/// </summary>
public enum PhoneUsage
{
    Any,
    Mobile,
}
