namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// El título de una opción que sale de un nombre (miembros o contactos): uno de más de <see cref="FormsTextLimits.OptionText"/>
/// caracteres se corta en 29 y se le agrega «…» (36); quien arma la opción pone el nombre entero en el detalle (11 y 21).
/// </summary>
public sealed record FormOptionTitle(string Text, bool IsCut)
{
    public const string Ellipsis = "…";

    public static FormOptionTitle From(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length <= FormsTextLimits.OptionText) return new FormOptionTitle(trimmed, IsCut: false);

        var length = FormsTextLimits.OptionText - Ellipsis.Length;
        // Nunca parte un carácter de dos mitades (un emoji, por ejemplo).
        if (char.IsHighSurrogate(trimmed[length - 1])) length--;
        return new FormOptionTitle(trimmed[..length].TrimEnd() + Ellipsis, IsCut: true);
    }
}
