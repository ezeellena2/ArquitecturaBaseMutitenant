using System.Globalization;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// El texto canónico del valor de una opción (C6): el número con punto, sin separador de miles ni ceros de más, y la fecha ISO.
/// Es un valor del contrato, no un texto para una persona: lo que se muestra lo formatea la pantalla con la cultura del pedido.
/// </summary>
public static class FormCatalogValueText
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>El número tal cual, con todos sus dígitos y sin notación científica: 1500, 2500.5, -7.</summary>
    public static string Number(decimal value) =>
        (value / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);

    public static string Date(DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// El valor guardado de una opción: la fecha o el número; <c>null</c> en uno de texto. Lo usan la entidad y las filas que
    /// proyecta el lector sin cargarla.
    /// </summary>
    public static string? Of(DateOnly? date, decimal? number) => date is { } day ? Date(day) : number is { } value ? Number(value) : null;

    /// <summary>
    /// Lee un número canónico, con punto y sin redondear (C6): un entero de hasta 18 dígitos o, si no es
    /// <paramref name="wholeNumber"/>, un decimal de hasta 18 enteros y 10 decimales; más decimales no entra, y los ceros de más no
    /// cuentan. Lo comparten el valor de una opción y el de un campo (E13).
    /// </summary>
    public static bool TryParseNumber(string value, bool wholeNumber, out decimal number)
    {
        ArgumentNullException.ThrowIfNull(value);
        number = 0;
        var digits = value.StartsWith('-') ? value[1..] : value;
        var point = digits.IndexOf('.', StringComparison.Ordinal);
        var integerPart = point < 0 ? digits : digits[..point];
        var decimalPart = point < 0 ? string.Empty : digits[(point + 1)..];
        if (integerPart.Length == 0 || !integerPart.All(char.IsAsciiDigit)) return false;
        if (point >= 0 && (wholeNumber || decimalPart.Length == 0 || !decimalPart.All(char.IsAsciiDigit))) return false;
        if (integerPart.TrimStart('0').Length > FormCatalogItem.MaxIntegerDigits || decimalPart.TrimEnd('0').Length > FormCatalogItem.MaxDecimalDigits)
            return false;

        number = decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Lee una fecha canónica, <c>yyyy-MM-dd</c>.</summary>
    public static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
