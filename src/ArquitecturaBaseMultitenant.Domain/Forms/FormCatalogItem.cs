using System.Globalization;
using System.Text;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Una opción de un catálogo a mano (E12, §3). El código es único por catálogo sin distinguir mayúsculas
/// (<see cref="CodeNormalized"/>), y la posición la maneja el servidor (C4). En un catálogo tipado lleva su valor, guardado canónico
/// (C6); en uno de texto, ninguno. El índice de cada método es el de la opción en el pedido (<c>items.{i}</c>), para el campo del
/// error. Borrarla es sacarla con el repositorio, después de comprobar que nada la usa (129).
/// </summary>
public sealed class FormCatalogItem : Entity, ITenantOwned, IAuditable
{
    /// <summary>Dígitos enteros de un valor numérico, y decimales de uno decimal (C6): entran en <c>numeric(28, 10)</c>.</summary>
    public const int MaxIntegerDigits = 18;
    public const int MaxDecimalDigits = 10;
    private const string DateFormat = "yyyy-MM-dd";

    private FormCatalogItem() { }
    public Guid TenantId { get; private set; }
    public Guid CatalogId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string CodeNormalized { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public string? Detail { get; private set; }
    public decimal? NumberValue { get; private set; }
    public DateOnly? DateValue { get; private set; }
    public int Position { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Crea una opción en un catálogo a mano; quien la crea ya comprobó que se pueden cambiar sus opciones.</summary>
    public static Result<FormCatalogItem> Create(FormCatalog catalog, int index, string code, string text, string? detail,
        string? value, bool isActive, int position)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Source != FormCatalogSource.Manual) throw new InvalidOperationException("Only a manual catalog has options.");
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        var item = new FormCatalogItem { CatalogId = catalog.Id, Position = position, IsActive = isActive };
        var edited = item.Apply(catalog.ValueType, index, code, text, detail, value);
        return edited.IsFailure ? edited.Error : item;
    }

    public Result Edit(FormCatalog catalog, int index, string code, string text, string? detail, string? value)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Id != CatalogId) throw new InvalidOperationException("The option belongs to another catalog.");
        return Apply(catalog.ValueType, index, code, text, detail, value);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Sube o baja intercambiando la posición con la vecina, bajo el bloqueo del catálogo (C4). Sin vecina, la primera no sube y la
    /// última no baja.
    /// </summary>
    public Result SwapPosition(FormCatalogItem? neighbor)
    {
        if (neighbor is null) return FormCatalogErrors.MoveOutOfRange;
        if (ReferenceEquals(neighbor, this) || neighbor.Id == Id) throw new InvalidOperationException("An option can't swap with itself.");
        if (neighbor.CatalogId != CatalogId) throw new InvalidOperationException("The neighbor belongs to another catalog.");
        (Position, neighbor.Position) = (neighbor.Position, Position);
        return Result.Success();
    }

    /// <summary>
    /// La deja en su lugar del orden que arma el servidor bajo el bloqueo del catálogo (C4): al ordenar todas (224), al cerrar el
    /// hueco de las borradas o al sumar una nueva al final. La restricción diferida admite que dos se crucen hasta confirmar.
    /// </summary>
    public void PlaceAt(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        Position = position;
    }

    /// <summary>El valor como texto canónico (C6): el número con punto y sin ceros de más, o la fecha ISO; <c>null</c> en uno de texto.</summary>
    public string? ValueText() => FormCatalogValueText.Of(DateValue, NumberValue);

    /// <summary>La clave del código único por catálogo, sin distinguir mayúsculas ni la forma de escribir un acento.</summary>
    public static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return code.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant().Normalize(NormalizationForm.FormC);
    }

    private Result Apply(FormCatalogValueType valueType, int index, string code, string text, string? detail, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var trimmedCode = code.Trim();
        if (trimmedCode.Length > FormsTextLimits.OptionCode) throw new ArgumentException("The code is too long.", nameof(code));
        var trimmedText = text.Trim();
        if (trimmedText.Length > FormsTextLimits.OptionText) return FormCatalogErrors.ItemTextTooLong(index);
        var trimmedDetail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        if (trimmedDetail?.Length > FormsTextLimits.OptionDetail) return FormCatalogErrors.ItemDetailTooLong(index);

        decimal? number = null;
        DateOnly? date = null;
        if (valueType != FormCatalogValueType.Text)
        {
            if (string.IsNullOrWhiteSpace(value)) return FormCatalogErrors.ValueRequired(index);
            var parsed = valueType == FormCatalogValueType.Date ? TryParseDate(value.Trim(), out date) : TryParseNumber(valueType, value.Trim(), out number);
            if (!parsed) return FormCatalogErrors.ValueInvalid(index, valueType);
        }

        Code = trimmedCode;
        CodeNormalized = NormalizeCode(trimmedCode);
        Text = trimmedText;
        Detail = trimmedDetail;
        NumberValue = number;
        DateValue = date;
        return Result.Success();
    }

    private static bool TryParseDate(string value, out DateOnly? date)
    {
        var parsed = DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day);
        date = parsed ? day : null;
        return parsed;
    }

    /// <summary>
    /// Lee el texto canónico, con punto, sin redondear: un entero de hasta 18 dígitos, o un decimal de hasta 18 enteros y 10
    /// decimales; más decimales se rechaza, y los ceros de más no cuentan.
    /// </summary>
    private static bool TryParseNumber(FormCatalogValueType valueType, string value, out decimal? number)
    {
        number = null;
        var digits = value.StartsWith('-') ? value[1..] : value;
        var point = digits.IndexOf('.', StringComparison.Ordinal);
        var integerPart = point < 0 ? digits : digits[..point];
        var decimalPart = point < 0 ? string.Empty : digits[(point + 1)..];
        if (integerPart.Length == 0 || !integerPart.All(char.IsAsciiDigit)) return false;
        if (point >= 0 && (valueType == FormCatalogValueType.WholeNumber || decimalPart.Length == 0 || !decimalPart.All(char.IsAsciiDigit)))
            return false;
        if (integerPart.TrimStart('0').Length > MaxIntegerDigits || decimalPart.TrimEnd('0').Length > MaxDecimalDigits) return false;

        number = decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return true;
    }
}
