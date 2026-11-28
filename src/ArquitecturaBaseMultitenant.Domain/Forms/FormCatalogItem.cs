using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Una opción de un catálogo a mano (E12, §3). El código es único por catálogo sin distinguir mayúsculas
/// (<see cref="CodeNormalized"/>), y la posición la maneja el servidor (C4). En un catálogo tipado lleva su valor, guardado canónico
/// (C6); en uno de texto, ninguno. El índice de cada método es el de la opción en el pedido (<c>items.{i}</c>), para el campo del
/// error. Borrarla es sacarla con el repositorio, después de comprobar que nada la usa (129). Desde la E13 lleva los valores de los
/// campos del catálogo (<see cref="FieldValuesJson"/>), por la clave de cada campo.
/// </summary>
public sealed class FormCatalogItem : Entity, ITenantOwned, IAuditable
{
    /// <summary>Dígitos enteros de un valor numérico, y decimales de uno decimal (C6): entran en <c>numeric(28, 10)</c>.</summary>
    public const int MaxIntegerDigits = 18;
    public const int MaxDecimalDigits = 10;

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
    /// <summary>Los valores de los campos como jsonb, un mapa de la clave del campo al valor canónico (E13).</summary>
    public string FieldValuesJson { get; private set; } = "{}";

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

    /// <summary>Los valores de sus campos por clave (E13), también los de los campos quitados.</summary>
    public IReadOnlyDictionary<string, JsonElement> ReadFieldValues() => FieldValuesFromJson(FieldValuesJson);

    /// <summary>Lee el jsonb de los valores, también para las filas que proyecta el lector sin cargar la opción.</summary>
    public static IReadOnlyDictionary<string, JsonElement> FieldValuesFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            ?? throw new InvalidOperationException("The option field values are empty.");
    }

    /// <summary>
    /// Aplica el delta de los valores de sus campos (E13), por la identidad del campo: uno ausente se conserva y <c>null</c> lo limpia.
    /// Cada valor llega canónico, como en el contrato (un número como texto con punto, la fecha <c>yyyy-MM-dd</c>, sí/no como booleano
    /// y una fuente como la identidad de su opción), y se guarda por la clave del campo; uno que no es de su tipo, que no cumple su
    /// límite (un texto sin regla, hasta 300) o de un campo que no está activo es <see cref="FormCatalogErrors.FieldValueInvalid"/>.
    /// Leer con la cultura del pedido, que la opción fuente exista y que un valor único no se repita lo hace el servicio antes. Una
    /// opción activa necesita un valor en cada campo obligatorio, también con un delta vacío, que es cómo se comprueba al reactivarla.
    /// Si nada cambió, la columna no cambia.
    /// </summary>
    /// <param name="catalog">Su catálogo, con los campos guardados.</param>
    /// <param name="index">El de la opción en el pedido (<c>items.{i}</c>), para el campo del error.</param>
    /// <param name="delta">Los valores que cambian, por la identidad del campo.</param>
    public Result SetFieldValues(FormCatalog catalog, int index, IReadOnlyDictionary<Guid, JsonElement?> delta)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(delta);
        if (catalog.Id != CatalogId) throw new InvalidOperationException("The option belongs to another catalog.");

        var fields = catalog.ReadFields();
        var values = new SortedDictionary<string, JsonElement>(ReadFieldValues().ToDictionary(), StringComparer.Ordinal);
        var current = JsonSerializer.Serialize(values);
        foreach (var (fieldId, value) in delta)
        {
            var field = fields.FirstOrDefault(candidate => candidate.Id == fieldId && candidate.IsActive);
            if (field is null) return FormCatalogErrors.FieldValueInvalid(index, fieldId, FormCatalogErrors.FieldValueInvalidCode);
            var checkedValue = field.Check(value ?? default);
            if (checkedValue.IsEmpty)
            {
                values.Remove(field.Key);
                continue;
            }

            if (checkedValue.Canonical is not { } canonical)
                return FormCatalogErrors.FieldValueInvalid(index, fieldId, FormCatalogErrors.FieldValueInvalidCode);
            values[field.Key] = canonical;
        }

        if (IsActive)
        {
            foreach (var field in fields.Where(field => field.IsActive && field.Required))
                if (!values.TryGetValue(field.Key, out var value) || (value.ValueKind == JsonValueKind.String && value.GetString()!.Length == 0))
                    return FormCatalogErrors.FieldValueRequired(index, field.Id);
        }

        var next = JsonSerializer.Serialize(values);
        if (!string.Equals(current, next, StringComparison.Ordinal)) FieldValuesJson = next;
        return Result.Success();
    }

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
            var trimmed = value.Trim();
            if (valueType == FormCatalogValueType.Date && FormCatalogValueText.TryParseDate(trimmed, out var day)) date = day;
            else if (valueType != FormCatalogValueType.Date
                && FormCatalogValueText.TryParseNumber(trimmed, valueType == FormCatalogValueType.WholeNumber, out var parsed)) number = parsed;
            else return FormCatalogErrors.ValueInvalid(index, valueType);
        }

        Code = trimmedCode;
        CodeNormalized = NormalizeCode(trimmedCode);
        Text = trimmedText;
        Detail = trimmedDetail;
        NumberValue = number;
        DateValue = date;
        return Result.Success();
    }
}
