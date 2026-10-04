namespace ArquitecturaBaseMultitenant.Application.Common.Text;

/// <summary>
/// Enmascara datos personales con el formato de los tableros: un correo conserva dos letras y el dominio
/// («lu•••@delta.ejemplo.com»). Lo que no es un correo se oculta entero. Los teléfonos siguen en
/// <c>IPhoneNumberParser.Mask</c> hasta la consolidación previa a la Etapa 7.
/// </summary>
public static class PersonalDataMask
{
    private const string Hidden = "•••";

    public static string Email(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var at = value.LastIndexOf('@');
        if (at < 1 || at == value.Length - 1) return Hidden;
        var visible = at > 2 ? 2 : 1;
        return string.Concat(value.AsSpan(0, visible), Hidden, value.AsSpan(at));
    }
}
