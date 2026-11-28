using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Lo que se ve de cada opción de un catálogo a mano en WhatsApp (E13): el título y la descripción que arma su presentación, con
/// los lugares que tienen contenido unidos con « · », y lo que no entra (<see cref="FormCatalogPresentationIssue"/>): el título
/// vacío, de más de 30 caracteres, o la descripción de más de 300, los topes del texto y del detalle de una opción. Cada lugar se
/// lee en la cultura del pedido, con <see cref="DisplayFormatter"/>: el número con sus miles y sus decimales, la fecha con su
/// patrón, «Sí» o «No», y en «De un catálogo» el texto de la opción de la fuente, también si está dada de baja. Un lugar de un campo
/// quitado sale de la presentación, y si el título se queda sin ninguno, vuelve al texto (<see cref="FormCatalogPresentation.WithoutFields"/>).
/// Lo arma igual el front, en «Por ejemplo» y en el teléfono (<c>buildPresentation</c>).
/// </summary>
internal sealed class FormCatalogPresentationBuilder(IFormCatalogReader reader, FormCatalogFieldValuePolicy values, DisplayFormatter formatter)
{
    /// <summary>Lo que une los lugares de un título o de una descripción.</summary>
    public const string Separator = " · ";

    /// <summary>Las opciones activas que lee cada vuelta de <see cref="IssuesAsync"/>.</summary>
    public const int BatchSize = 500;

    private static readonly IReadOnlyDictionary<Guid, JsonElement> NoValues = new Dictionary<Guid, JsonElement>();

    /// <summary>
    /// Une los lugares con contenido, sin los espacios de sus extremos, y dice qué no entra: primero el título vacío, después el
    /// título largo y por último la descripción larga.
    /// </summary>
    /// <param name="presentation">Los lugares del título y de la descripción.</param>
    /// <param name="placeText">Lo que dice cada lugar en la opción, ya formateado; <c>null</c> o vacío si no tiene contenido.</param>
    public static FormCatalogBuiltPresentation Build(FormCatalogPresentation presentation, Func<string, string?> placeText)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(placeText);
        string Join(IReadOnlyList<string> places) =>
            string.Join(Separator, places.Select(place => placeText(place)?.Trim()).Where(text => !string.IsNullOrEmpty(text)));

        var title = Join(presentation.Title);
        var description = Join(presentation.Description);
        FormCatalogPresentationIssue? issue = title.Length == 0 ? FormCatalogPresentationIssue.TitleEmpty
            : title.Length > FormsTextLimits.OptionText ? FormCatalogPresentationIssue.TitleTooLong
            : description.Length > FormsTextLimits.OptionDetail ? FormCatalogPresentationIssue.DescriptionTooLong
            : null;
        return new FormCatalogBuiltPresentation(title, description, issue);
    }

    /// <summary>
    /// Lo que se muestra de verdad: la presentación sin los lugares de los campos que ya no están activos (si el título se queda sin
    /// ninguno, vuelve al texto) y los campos activos que usa, por su identidad.
    /// </summary>
    public static FormCatalogPresentationLayout Layout(FormCatalogPresentation presentation, IReadOnlyList<FormCatalogField> fields,
        FormCatalogValueType valueType)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(fields);
        var active = fields.Where(field => field.IsActive).ToDictionary(field => field.Id);
        var gone = FieldIds(presentation).Where(id => !active.ContainsKey(id)).ToHashSet();
        var shown = gone.Count == 0 ? presentation : presentation.WithoutFields(gone);
        return new FormCatalogPresentationLayout(shown, FieldIds(shown).ToDictionary(id => id, id => active[id]), valueType);
    }

    /// <summary>
    /// Cuántas opciones activas no entran y hasta tres de ejemplo, en orden de posición, leídas en lotes de <see cref="BatchSize"/>.
    /// Cada ejemplo dice su título o, si queda vacío, su texto. Uno que no es a mano, sin opciones activas, o con una presentación que
    /// no puede pasarse (<see cref="FormCatalogPresentationLayout.CanFail"/>), no lee opciones.
    /// </summary>
    public async Task<FormCatalogPresentationIssues> IssuesAsync(FormCatalogStoredRow catalog, FormCatalogPresentation presentation,
        IReadOnlyList<FormCatalogField> fields, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Source != FormCatalogSource.Manual || catalog.ActiveItems == 0) return FormCatalogPresentationIssues.None;
        var layout = Layout(presentation, fields, catalog.ValueType);
        if (!layout.CanFail) return FormCatalogPresentationIssues.None;

        var culture = await values.CultureAsync(ct);
        var count = 0;
        var examples = new List<FormCatalogPresentationExample>(FormCatalogLimits.PresentationExamples);
        int? after = null;
        IReadOnlyList<FormCatalogStoredItemRow> batch;
        do
        {
            batch = await reader.ListActiveItemBatchAsync(catalog.Id, after, BatchSize, ct);
            if (batch.Count == 0) break;
            var built = await BuildAsync(layout, batch, culture, ct);
            for (var index = 0; index < batch.Count; index++)
            {
                if (built[index].Issue is not { } issue) continue;
                count++;
                if (examples.Count < FormCatalogLimits.PresentationExamples)
                    examples.Add(new FormCatalogPresentationExample(batch[index].Id,
                        built[index].Title.Length > 0 ? built[index].Title : batch[index].Text, issue));
            }

            after = batch[^1].Position;
        }
        while (batch.Count == BatchSize);
        return new FormCatalogPresentationIssues(count, examples);
    }

    /// <summary>El título y la descripción de cada opción guardada, en el mismo orden, en la cultura del pedido.</summary>
    public Task<IReadOnlyList<FormCatalogBuiltPresentation>> BuildAsync(FormCatalogPresentationLayout layout,
        IReadOnlyList<FormCatalogStoredItemRow> rows, DisplayFormatContext culture, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(rows);
        return BuildAsync(layout, [.. rows.Select(row => Presented.Of(row, layout))], culture, ct);
    }

    /// <summary>La página de opciones con lo que no entra de cada una, activa o dada de baja (<c>presentationIssue</c>).</summary>
    public async Task<PagedResult<FormCatalogItemRow>> WithIssuesAsync(FormCatalogStoredRow catalog, PagedResult<FormCatalogItemRow> page,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(page);
        if (catalog.Source != FormCatalogSource.Manual || page.Items.Count == 0) return page;
        if (await reader.FindPresentationAsync(catalog.Id, ct) is not { } stored) return page;
        var layout = Layout(FormCatalogPresentation.FromJson(stored.Presentation), FormCatalogField.ListFromJson(stored.FieldsJson),
            catalog.ValueType);
        if (!layout.CanFail) return page;

        Presented[] options = [.. page.Items.Select(row => new Presented(row.Code, row.Text, row.Detail, row.Value, row.Fields))];
        var built = await BuildAsync(layout, options, await values.CultureAsync(ct), ct);
        return page with { Items = [.. page.Items.Zip(built, (row, shown) => row with { PresentationIssue = shown.Issue })] };
    }

    private async Task<IReadOnlyList<FormCatalogBuiltPresentation>> BuildAsync(FormCatalogPresentationLayout layout,
        IReadOnlyList<Presented> options, DisplayFormatContext culture, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var texts = await OptionTextsAsync(layout, options, ct);
        return [.. options.Select(option => Build(layout, option, culture, texts))];
    }

    private FormCatalogBuiltPresentation Build(FormCatalogPresentationLayout layout, Presented option, DisplayFormatContext culture,
        IReadOnlyDictionary<(Guid Source, Guid Option), string> texts) => Build(layout.Presentation, place => place switch
        {
            FormCatalogPresentation.Text => option.Text,
            FormCatalogPresentation.Code => option.Code,
            FormCatalogPresentation.Detail => option.Detail,
            FormCatalogPresentation.Value => ValueText(option.Value, layout.ValueType, culture),
            _ => FormCatalogPresentation.FieldIdOf(place) is { } id && layout.Fields.TryGetValue(id, out var field)
                && option.Fields.TryGetValue(id, out var value)
                ? values.TextOf(field, value, culture, optionId => texts.GetValueOrDefault((field.SourceCatalogId ?? Guid.Empty, optionId)))
                : null,
        });

    /// <summary>El valor de un catálogo tipado como se lee: el número con sus miles y sus decimales, y la fecha con su patrón.</summary>
    private string? ValueText(string? canonical, FormCatalogValueType valueType, DisplayFormatContext culture)
    {
        if (canonical is null) return null;
        if (valueType == FormCatalogValueType.Date)
            return FormCatalogValueText.TryParseDate(canonical, out var date) ? formatter.FormatDate(date, culture) : canonical;
        if (valueType is not (FormCatalogValueType.WholeNumber or FormCatalogValueType.DecimalNumber)
            || !decimal.TryParse(canonical, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            return canonical;
        var point = canonical.IndexOf('.', StringComparison.Ordinal);
        return formatter.FormatDecimal(number, point < 0 ? 0 : canonical.Length - point - 1, culture);
    }

    /// <summary>
    /// Los textos de las opciones de otro catálogo que son valor de un campo «De un catálogo» de la presentación, activas o dadas de
    /// baja, en una consulta por fuente.
    /// </summary>
    private async Task<IReadOnlyDictionary<(Guid Source, Guid Option), string>> OptionTextsAsync(FormCatalogPresentationLayout layout,
        IReadOnlyList<Presented> options, CancellationToken ct)
    {
        var texts = new Dictionary<(Guid Source, Guid Option), string>();
        var wanted = layout.Fields.Values
            .Where(field => field.Type == FormCatalogFieldType.Catalog && field.SourceCatalogId is not null)
            .SelectMany(field => options.Select(option => OptionIdOf(option, field)).OfType<Guid>().Select(id => (Source: field.SourceCatalogId!.Value, Option: id)))
            .GroupBy(pair => pair.Source, pair => pair.Option);
        foreach (var source in wanted)
            foreach (var row in await reader.ListItemsByIdsAsync(source.Key, [.. source.Distinct()], ct))
                texts[(source.Key, row.Id)] = row.Text;
        return texts;
    }

    private static Guid? OptionIdOf(Presented option, FormCatalogField field) =>
        option.Fields.TryGetValue(field.Id, out var value) && value.ValueKind == JsonValueKind.String
        && Guid.TryParse(value.GetString(), out var id) ? id : null;

    private static IEnumerable<Guid> FieldIds(FormCatalogPresentation presentation) =>
        presentation.Title.Concat(presentation.Description).Select(FormCatalogPresentation.FieldIdOf).OfType<Guid>().Distinct();

    /// <summary>Una opción con lo que puede mostrar: sus columnas y los valores de sus campos por la identidad del campo.</summary>
    private sealed record Presented(string Code, string Text, string? Detail, string? Value, IReadOnlyDictionary<Guid, JsonElement> Fields)
    {
        /// <summary>Una opción guardada, con los valores de los campos que usa la presentación, que guarda por su clave.</summary>
        public static Presented Of(FormCatalogStoredItemRow row, FormCatalogPresentationLayout layout)
        {
            var fields = NoValues;
            if (layout.Fields.Count > 0)
            {
                var byKey = FormCatalogItem.FieldValuesFromJson(row.FieldValuesJson);
                fields = layout.Fields.Values.Where(field => byKey.ContainsKey(field.Key)).ToDictionary(field => field.Id, field => byKey[field.Key]);
            }

            return new Presented(row.Code, row.Text, row.Detail, row.Value, fields);
        }

        public override string ToString() => nameof(Presented);
    }
}

/// <summary>El título y la descripción de una opción como se ven en WhatsApp, y lo que no entra (<c>null</c> si entra todo).</summary>
internal sealed record FormCatalogBuiltPresentation(string Title, string Description, FormCatalogPresentationIssue? Issue)
{
    public override string ToString() => nameof(FormCatalogBuiltPresentation);
}

/// <summary>
/// La presentación que se muestra, sin los campos que ya no están, con los campos activos que usa por su identidad y el tipo del
/// valor del catálogo.
/// </summary>
internal sealed record FormCatalogPresentationLayout(FormCatalogPresentation Presentation, IReadOnlyDictionary<Guid, FormCatalogField> Fields,
    FormCatalogValueType ValueType)
{
    /// <summary>
    /// Si alguna opción puede no entrar. Con el texto solo en el título y a lo sumo el detalle en la descripción, ninguna: el texto
    /// es obligatorio y tiene hasta 30 caracteres, y el detalle hasta 300, como la presentación de un catálogo nuevo.
    /// </summary>
    public bool CanFail => Presentation.Title is not [FormCatalogPresentation.Text]
        || Presentation.Description is not ([] or [FormCatalogPresentation.Detail]);

    public override string ToString() => nameof(FormCatalogPresentationLayout);
}
