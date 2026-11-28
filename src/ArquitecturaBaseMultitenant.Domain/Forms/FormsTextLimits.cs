namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Los topes de texto de Formularios que <see cref="Common.TextLimits"/> no tiene (E12, §3); los comparten el dominio, los
/// validadores y los mapeos de EF, y la pantalla usa los mismos (269).
/// </summary>
public static class FormsTextLimits
{
    /// <summary>El texto de una opción, que es su título en WhatsApp; un nombre más largo se corta con <see cref="FormOptionTitle"/>.</summary>
    public const int OptionText = 30;
    /// <summary>El código de una opción, que no se ve en WhatsApp.</summary>
    public const int OptionCode = 40;
    /// <summary>El detalle de una opción, su descripción en WhatsApp.</summary>
    public const int OptionDetail = 300;
    /// <summary>La descripción de un catálogo.</summary>
    public const int CatalogDescription = 300;
    /// <summary>El nombre de un campo de las opciones (E13), un solo texto.</summary>
    public const int FieldName = 35;
    /// <summary>La clave de un campo, que nace del nombre y queda fija al guardar (E13).</summary>
    public const int FieldKey = 40;
    /// <summary>El largo máximo que admite la regla de un campo de texto, de 1 a este (E13).</summary>
    public const int FieldTextLength = 300;
}
