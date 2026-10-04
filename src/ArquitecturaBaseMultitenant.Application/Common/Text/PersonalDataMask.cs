using System.Buffers;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Common.Text;

/// <summary>
/// La única forma de enmascarar un correo o un teléfono, en pantallas, avisos, auditoría y logs. Un correo conserva
/// dos letras y el dominio («lu•••@delta.ejemplo.com»), como los tableros de Plataforma y Mi cuenta. Un teléfono
/// conserva solo los últimos cuatro dígitos («•••• 4521»): la característica que muestran los tableros sale del
/// formato del país, que vive en libphonenumber, y esta pieza no lo conoce. Lo que ya viene enmascarado no muestra más.
/// </summary>
public static class PersonalDataMask
{
    private const string HiddenText = "•••";
    private const string HiddenDigits = "••••";
    private const int VisiblePhoneDigits = 4;
    private static readonly SearchValues<char> MaskCharacters = SearchValues.Create("•*");

    public static string Email(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var at = value.LastIndexOf('@');
        if (at < 1 || at == value.Length - 1) return HiddenText;
        var visible = at > 2 ? 2 : 1;
        var masked = value.AsSpan(0, at).IndexOfAny(MaskCharacters);
        if (masked >= 0 && masked < visible) visible = masked;
        return string.Concat(value.AsSpan(0, visible), HiddenText, value.AsSpan(at));
    }

    public static string Phone(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Span<char> last = stackalloc char[VisiblePhoneDigits];
        var found = 0;
        for (var index = value.Length - 1; index >= 0 && found < VisiblePhoneDigits; index--)
        {
            if (char.IsAsciiDigit(value[index])) last[VisiblePhoneDigits - ++found] = value[index];
        }

        return found < VisiblePhoneDigits ? HiddenDigits : string.Concat(HiddenDigits, " ", last);
    }

    /// <summary>Un método de ingreso según su tipo. Google se muestra por su correo de contacto, nunca por su subject.</summary>
    public static string LoginMethod(LoginMethodType type, string value, string? contactEmail) => type switch
    {
        LoginMethodType.Phone => Phone(value),
        LoginMethodType.Google => contactEmail is null ? "Google" : Email(contactEmail),
        _ => Email(value),
    };
}
