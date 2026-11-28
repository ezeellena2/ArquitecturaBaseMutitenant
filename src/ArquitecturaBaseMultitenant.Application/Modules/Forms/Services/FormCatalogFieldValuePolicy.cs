using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Lee y valida el valor de un campo de un catálogo a mano (E13), sin escribir. La regla de cada tipo es una sola,
/// <see cref="FormCatalogField.Check"/>; acá se suma lo que depende del pedido:
/// <list type="bullet">
/// <item>lo que escribe una persona (una celda, o el texto guardado que cambia de tipo) se lee con la cultura del pedido, como el
/// valor de la E12 (C9): lo que se lee de dos formas, como «1.500» en español, no se lee;</item>
/// <item>un valor nuevo de «De un catálogo» pide una opción activa de su fuente, y el guardado que apunta a una dada de baja se
/// conserva sin volver a leerlo (lección 1);</item>
/// <item>el mensaje de un valor que no sirve: el de su tipo, como en la E12, o su largo o su rango, en la cultura del pedido.</item>
/// </list>
/// Que un valor único no se repita lo decide quien guarda, con todas las opciones activas.
/// </summary>
internal sealed class FormCatalogFieldValuePolicy(DisplayFormatter formatter, IFormCatalogReader catalogs)
{
    /// <summary>Las opciones de una fuente que se leen por consulta.</summary>
    private const int OptionsPerRead = 1_000;

    /// <summary>La cultura del pedido, una vez por pedido: con ella se lee lo que escribe una persona y se arma el mensaje.</summary>
    public Task<DisplayFormatContext> CultureAsync(CancellationToken ct) =>
        formatter.CreateForCurrentCultureAsync(TimeZoneInfo.Utc.Id, ct);

    /// <summary>
    /// Lo que escribió una persona, leído con la cultura del pedido: el número y la fecha como en la E12 (también su texto canónico),
    /// sí o no con las palabras de la cultura o <c>true</c> y <c>false</c>, y una fuente por la identidad de su opción. En un texto
    /// queda lo escrito, también <c>""</c>; en los demás, un texto en blanco no es un valor.
    /// </summary>
    public static FormCatalogFieldValue Read(FormCatalogField field, string text, DisplayFormatContext culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return Read(field, text, culture.Profile);
    }

    /// <summary>Lo mismo con el perfil de la cultura ya cargado: el de quien lee un archivo (B7).</summary>
    public static FormCatalogFieldValue Read(FormCatalogField field, string text, CultureProfile culture)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);
        var trimmed = text.Trim();
        if (field.Type == FormCatalogFieldType.Text) return field.Check(JsonSerializer.SerializeToElement(trimmed));
        if (trimmed.Length == 0) return FormCatalogFieldValue.Empty;

        switch (field.Type)
        {
            case FormCatalogFieldType.WholeNumber or FormCatalogFieldType.DecimalNumber or FormCatalogFieldType.Date:
                var valueType = field.Type switch
                {
                    FormCatalogFieldType.WholeNumber => FormCatalogValueType.WholeNumber,
                    FormCatalogFieldType.DecimalNumber => FormCatalogValueType.DecimalNumber,
                    _ => FormCatalogValueType.Date,
                };
                var read = FormCatalogFileResolver.ReadValue(trimmed, valueType, culture.Entry);
                if (read.Canonical is { } canonical) return field.Check(JsonSerializer.SerializeToElement(canonical));
                return FormCatalogFieldValue.Failed(read.ErrorCode == FormCatalogImportErrors.ValueAmbiguousCode
                    ? FormCatalogFieldValueIssue.Ambiguous : FormCatalogFieldValueIssue.Invalid);
            case FormCatalogFieldType.YesNo:
                return YesOrNo(trimmed, culture) is { } yes ? field.Check(JsonSerializer.SerializeToElement(yes))
                    : FormCatalogFieldValue.Failed(FormCatalogFieldValueIssue.Invalid);
            default:
                return field.Check(JsonSerializer.SerializeToElement(trimmed));
        }
    }

    /// <summary>
    /// Si cada valor de «De un catálogo» sirve, en el mismo orden: el que ya estaba guardado se conserva aunque su opción esté dada
    /// de baja, y uno nuevo tiene que ser una opción activa de la fuente de su campo. Las opciones se leen de una vez por fuente.
    /// </summary>
    public async Task<IReadOnlyList<bool>> CheckOptionsAsync(IReadOnlyList<FormCatalogFieldOptionValue> values, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(values);
        var accepted = new bool[values.Count];
        var pending = new List<int>();
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (value.Field.Type != FormCatalogFieldType.Catalog || value.Field.SourceCatalogId is null)
                throw new InvalidOperationException("The field doesn't take its values from a catalog.");
            if (value.OptionId == value.StoredOptionId) accepted[index] = true;
            else pending.Add(index);
        }

        foreach (var source in pending.GroupBy(index => values[index].Field.SourceCatalogId!.Value))
        {
            var active = new HashSet<Guid>();
            foreach (var ids in source.Select(index => values[index].OptionId).Distinct().Chunk(OptionsPerRead))
                active.UnionWith((await catalogs.ListItemsByIdsAsync(source.Key, ids, ct)).Where(option => option.IsActive).Select(option => option.Id));
            foreach (var index in source) accepted[index] = active.Contains(values[index].OptionId);
        }

        return accepted;
    }

    /// <summary>
    /// El texto que se ve de un valor canónico, en la cultura del pedido: el número con sus miles y sus decimales, la fecha con su
    /// patrón, «Sí» o «No», el texto y, en «De un catálogo», el de su opción, que da <paramref name="optionText"/> también si está
    /// dada de baja. <c>null</c> si no se puede leer.
    /// </summary>
    public string? TextOf(FormCatalogField field, JsonElement value, DisplayFormatContext culture, Func<Guid, string?> optionText)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(optionText);
        switch (field.Type)
        {
            case FormCatalogFieldType.Text when value.ValueKind == JsonValueKind.String:
                return value.GetString();
            case FormCatalogFieldType.YesNo when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                return YesOrNoText(value.GetBoolean(), culture);
            case FormCatalogFieldType.WholeNumber or FormCatalogFieldType.DecimalNumber when value.ValueKind == JsonValueKind.String
                && decimal.TryParse(value.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number):
                var canonical = value.GetString()!;
                var point = canonical.IndexOf('.', StringComparison.Ordinal);
                return formatter.FormatDecimal(number, point < 0 ? 0 : canonical.Length - point - 1, culture);
            case FormCatalogFieldType.Date when value.ValueKind == JsonValueKind.String && FormCatalogValueText.TryParseDate(value.GetString()!, out var date):
                return formatter.FormatDate(date, culture);
            case FormCatalogFieldType.Catalog when value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var optionId):
                return optionText(optionId);
            default:
                return null;
        }
    }

    /// <summary>«Sí» o «No» en la cultura del pedido: lo que se ve, y se vuelve a leer, de un valor sí/no.</summary>
    public string YesOrNoText(bool value, DisplayFormatContext culture) => formatter.FormatBoolean(value, culture);

    /// <summary>
    /// <see cref="FormCatalogErrors.FieldValueInvalid"/> de la opción <paramref name="index"/>, con su mensaje: el largo o el rango,
    /// armados en la cultura del pedido; lo que se lee de dos formas, como en la E12; y si no es de su tipo, qué escribir en un
    /// número o una fecha, como en la E12, o el texto del campo. Un rango con un solo límite dice el texto del campo.
    /// </summary>
    public Error ErrorOf(int index, FormCatalogField field, FormCatalogFieldValueIssue issue, DisplayFormatContext culture)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(culture);
        var message = issue switch
        {
            FormCatalogFieldValueIssue.Length => FormsTexts.Get(FormCatalogErrors.FieldValueInvalidCode + ".Length", culture.Profile)
                .Replace("{n}", formatter.FormatDecimal(field.MaxLength ?? FormsTextLimits.FieldTextLength, 0, culture), StringComparison.Ordinal),
            FormCatalogFieldValueIssue.Range when field.Min is { } min && field.Max is { } max =>
                FormsTexts.Get(FormCatalogErrors.FieldValueInvalidCode + ".Range", culture.Profile)
                    .Replace("{min}", Limit(field, min, culture), StringComparison.Ordinal)
                    .Replace("{max}", Limit(field, max, culture), StringComparison.Ordinal),
            FormCatalogFieldValueIssue.Ambiguous => FormCatalogImportErrors.ValueAmbiguousCode,
            FormCatalogFieldValueIssue.Invalid when field.Type == FormCatalogFieldType.WholeNumber => FormCatalogErrors.ValueInvalidCode + "." + FormCatalogValueType.WholeNumber,
            FormCatalogFieldValueIssue.Invalid when field.Type == FormCatalogFieldType.DecimalNumber => FormCatalogErrors.ValueInvalidCode + "." + FormCatalogValueType.DecimalNumber,
            FormCatalogFieldValueIssue.Invalid when field.Type == FormCatalogFieldType.Date => FormCatalogErrors.ValueInvalidCode + "." + FormCatalogValueType.Date,
            _ => FormCatalogErrors.FieldValueInvalidCode,
        };
        return FormCatalogErrors.FieldValueInvalid(index, field.Id, message);
    }

    /// <summary>
    /// <see cref="FormCatalogErrors.FieldValueDuplicate"/> de la opción <paramref name="index"/>: «Ya lo tiene la opción {n}», con el
    /// número de la otra (su posición más uno) en la cultura del pedido.
    /// </summary>
    public Error DuplicateErrorOf(int index, FormCatalogField field, int optionNumber, DisplayFormatContext culture)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(culture);
        var message = FormsTexts.Get(FormCatalogErrors.FieldValueDuplicateCode, culture.Profile)
            .Replace("{n}", formatter.FormatDecimal(optionNumber, 0, culture), StringComparison.Ordinal);
        return FormCatalogErrors.FieldValueDuplicate(index, field.Id, message);
    }

    /// <summary>
    /// <see cref="FormCatalogErrors.FieldRemovalNotConfirmed"/> del campo <paramref name="index"/>: «Confirmá que se quitan sus {count}
    /// valores.», o el de un solo valor, con la cantidad en la cultura del pedido.
    /// </summary>
    public Error RemovalErrorOf(int index, int count, DisplayFormatContext culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        const string code = FormCatalogErrors.FieldRemovalNotConfirmedCode;
        var message = FormsTexts.Get(count == 1 ? code + ".Singular" : code, culture.Profile)
            .Replace("{count}", formatter.FormatDecimal(count, 0, culture), StringComparison.Ordinal);
        return FormCatalogErrors.FieldRemovalNotConfirmed(index, message);
    }

    /// <summary>
    /// <see cref="FormCatalogErrors.FieldKeyDuplicate"/>: «Ya la usa «{name}».», con el nombre del otro campo, en la cultura del pedido.
    /// </summary>
    public static string KeyTakenBy(string otherName, DisplayFormatContext culture)
    {
        ArgumentNullException.ThrowIfNull(otherName);
        ArgumentNullException.ThrowIfNull(culture);
        return FormsTexts.Get(FormCatalogErrors.FieldKeyDuplicateCode, culture.Profile).Replace("{name}", otherName, StringComparison.Ordinal);
    }

    /// <summary>Un límite canónico como se lee en la cultura: el número con sus miles y sus decimales, y la fecha con su patrón.</summary>
    private string Limit(FormCatalogField field, string canonical, DisplayFormatContext culture)
    {
        if (field.Type == FormCatalogFieldType.Date && FormCatalogValueText.TryParseDate(canonical, out var date))
            return formatter.FormatDate(date, culture);
        var point = canonical.IndexOf('.', StringComparison.Ordinal);
        var number = decimal.Parse(canonical, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return formatter.FormatDecimal(number, point < 0 ? 0 : canonical.Length - point - 1, culture);
    }

    /// <summary>Sí o no con las palabras de la cultura, sin mirar mayúsculas ni acentos («si» es «Sí»), o su forma canónica.</summary>
    private static bool? YesOrNo(string text, CultureProfile profile)
    {
        bool Is(string word) => profile.Culture.CompareInfo.Compare(text, word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;
        if (Is(FormattingTexts.Get("Boolean.True", profile)) || Is(bool.TrueString)) return true;
        if (Is(FormattingTexts.Get("Boolean.False", profile)) || Is(bool.FalseString)) return false;
        return null;
    }
}

/// <summary>
/// Un valor de «De un catálogo» que se va a guardar: la opción que nombra y la que tenía guardada esa opción en ese campo, si tenía.
/// </summary>
internal sealed record FormCatalogFieldOptionValue(FormCatalogField Field, Guid OptionId, Guid? StoredOptionId);
