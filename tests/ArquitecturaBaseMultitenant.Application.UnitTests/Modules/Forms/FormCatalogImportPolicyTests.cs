using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// La carga de una vez (C7 a C10): la lectura de números y fechas con la cultura del pedido y lo ambiguo, el plan de importación
/// (altas, cambios, bajas, columna ausente y columna vacía, errores y confirmaciones, con la segunda página de las listas) y las
/// celdas que exporta, con un número de 16 cifras y una fecha anterior a 1900 como texto canónico.
/// </summary>
public sealed class FormCatalogImportPolicyTests
{
    private static readonly CultureCatalogEntry Spanish = Culture("es-AR", "dd/MM/yyyy", ",", ".");
    private static readonly CultureCatalogEntry English = Culture("en-US", "MM/dd/yyyy", ".", ",");

    [Theory]
    [InlineData("1.500", "es", null, true)]
    [InlineData("12.345", "es", null, true)]
    [InlineData("1,500", "es", "1.500", false)]
    [InlineData("1.234,5", "es", "1234.5", false)]
    [InlineData("1,500", "en", null, true)]
    [InlineData("1.500", "en", "1.500", false)]
    [InlineData("1,234.5", "en", "1234.5", false)]
    [InlineData("1234567890123456.5", "es", "1234567890123456.5", false)]
    public void A_number_is_read_with_the_request_culture_and_one_that_reads_two_ways_is_ambiguous(string raw, string language,
        string? canonical, bool ambiguous)
    {
        var value = FormCatalogFileResolver.ReadValue(raw, FormCatalogValueType.DecimalNumber, language == "es" ? Spanish : English);

        Assert.Equal(canonical, value.Canonical);
        Assert.Equal(ambiguous ? FormCatalogImportErrors.ValueAmbiguousCode : null, value.ErrorCode);
        if (ambiguous) Assert.Equal(raw, value.Raw);
    }

    [Theory]
    [InlineData("3/4/2026", "es", null, true)]
    [InlineData("27/09/2026", "es", "2026-09-27", false)]
    [InlineData("09/27/2026", "en", "2026-09-27", false)]
    [InlineData("3/3/2026", "en", "2026-03-03", false)]
    [InlineData("1850-02-01", "en", "1850-02-01", false)]
    public void A_date_is_read_with_the_request_culture_and_a_swappable_day_and_month_is_ambiguous(string raw, string language,
        string? canonical, bool ambiguous)
    {
        var value = FormCatalogFileResolver.ReadValue(raw, FormCatalogValueType.Date, language == "es" ? Spanish : English);

        Assert.Equal(canonical, value.Canonical);
        Assert.Equal(ambiguous ? FormCatalogImportErrors.ValueAmbiguousCode : null, value.ErrorCode);
    }

    [Fact]
    public void Replacing_adds_changes_and_deactivates_what_did_not_come()
    {
        var saved = new[] { Saved("A", "Uno", 0), Saved("B", "Dos", 1, detail: "Viejo"), Saved("C", "Tres", 2), Saved("D", "Cuatro", 3) };
        var file = File(hasDetail: true, Row(2, "a", "Uno", null), Row(3, "B", "Dos", "Nuevo"), Row(4, "E", "Cinco", null));

        var plan = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Replace, file, saved, activeItems: 4);

        Assert.Equal(3, plan.RowsRead);
        Assert.Equal(0, plan.Unchanged);
        Assert.Equal([new FormCatalogImportAddedRow(4, "E", "Cinco")], plan.Lists.Added);
        Assert.Equal([new FormCatalogImportChangedRow(2, "a", "code", "A", "a"), new FormCatalogImportChangedRow(3, "B", "detail", "Viejo", "Nuevo")],
            plan.Lists.Changed);
        Assert.Equal(["C", "D"], plan.Lists.Deactivated.Select(row => row.Code));
        Assert.Equal(0.5m, plan.DeactivationShare);
        Assert.True(plan.ConfirmDeactivations);
        Assert.Equal(3, plan.Writes.Count);
    }

    [Fact]
    public void A_missing_optional_column_keeps_what_is_saved_and_a_present_empty_one_clears_it()
    {
        var saved = new[] { Saved("A", "Uno", 0, detail: "Guardado") };

        var missing = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Append,
            File(hasDetail: false, Row(2, "A", "Uno", null)), saved, 1);
        var empty = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Append,
            File(hasDetail: true, Row(2, "A", "Uno", null)), saved, 1);

        Assert.Equal(1, missing.Unchanged);
        Assert.Empty(missing.Writes);
        Assert.Equal([new FormCatalogImportChangedRow(2, "A", "detail", "Guardado", null)], empty.Lists.Changed);
    }

    [Fact]
    public void Pasting_appends_without_deactivating_and_a_missing_text_is_kept_only_for_a_saved_option()
    {
        var saved = new[] { Saved("A", "Uno", 0), Saved("B", "Dos", 1) };
        var file = new FormCatalogFile("hash", null, true, false, false, false, [Row(2, "A", null, null), Row(3, "Z", null, null)]);

        var plan = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Append, file, saved, 2);

        Assert.Empty(plan.Lists.Deactivated);
        Assert.Equal(1, plan.Unchanged);
        Assert.Equal([new FormCatalogImportErrorRow(3, "text", FormCatalogImportErrors.CellRequiredCode, null, null)], plan.Lists.Errors);
    }

    [Fact]
    public void Row_errors_go_by_row_and_column_and_a_code_in_a_row_with_errors_is_not_deactivated()
    {
        var saved = new[] { Saved("A", "Uno", 0), Saved("B", "Dos", 1) };
        var file = File(hasDetail: false, Row(2, "A", new string('x', 31), null), Row(3, "B", "Dos", null), Row(4, "b", "Otra", null),
            Row(5, null, "Sin código", null));

        var plan = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Replace, file, saved, 2);

        Assert.Equal(
        [
            new FormCatalogImportErrorRow(2, "text", FormCatalogErrors.ItemTextTooLongCode, null, null),
            new FormCatalogImportErrorRow(4, "code", FormCatalogErrors.DuplicateCodeCode, null, 3),
            new FormCatalogImportErrorRow(5, "code", FormCatalogImportErrors.CellRequiredCode, null, null),
        ], plan.Lists.Errors);
        Assert.Empty(plan.Lists.Deactivated);
        Assert.Equal(0.75m, plan.ErrorShare);
        Assert.True(plan.ConfirmErrors);
    }

    [Fact]
    public void A_typed_value_is_required_for_a_new_option_and_an_ambiguous_one_keeps_what_the_cell_said()
    {
        var file = new FormCatalogFile("hash", null, true, true, false, true,
        [
            new FormCatalogFileRow(2, "A", "Uno", null, FormCatalogFileValue.Read("1500")),
            new FormCatalogFileRow(3, "B", "Dos", null, FormCatalogFileValue.Empty),
            new FormCatalogFileRow(4, "C", "Tres", null, FormCatalogFileValue.Ambiguous("1.500")),
            new FormCatalogFileRow(5, "D", "Cuatro", null, FormCatalogFileValue.Read("1.5")),
        ]);

        var plan = FormCatalogImportPolicy.Decide(FormCatalogValueType.WholeNumber, FormCatalogImportMode.Append, file, [], 0);

        Assert.Equal(
        [
            new FormCatalogImportErrorRow(3, "value", FormCatalogImportErrors.CellRequiredCode, null, null),
            new FormCatalogImportErrorRow(4, "value", FormCatalogImportErrors.ValueAmbiguousCode, "1.500", null),
            new FormCatalogImportErrorRow(5, "value", FormCatalogErrors.ValueInvalidCode, null, null),
        ], plan.Lists.Errors);
        Assert.Equal("1500", Assert.Single(plan.Writes).Value);
    }

    [Fact]
    public void The_review_pages_its_four_lists_with_the_page_and_size_of_the_request()
    {
        var saved = Enumerable.Range(0, 12).Select(index => Saved($"S{index:00}", $"Guardada {index}", index)).ToArray();
        var rows = Enumerable.Range(0, 12).Select(index => Row(index + 2, $"N{index:00}", $"Nueva {index}", null)).ToArray();

        var plan = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Replace, File(false, rows), saved, 12);
        var second = plan.ToPreview(7, "hash", "Opciones", page: 2, pageSize: 10);

        Assert.Equal((2, 10, 12), (second.Added.Page, second.Added.PageSize, second.Added.TotalCount));
        Assert.Equal(["N10", "N11"], second.Added.Items.Select(row => row.Code));
        Assert.Equal(["S10", "S11"], second.Deactivated.Items.Select(row => row.Code));
        Assert.Equal(1m, second.DeactivationShare);
        Assert.True(second.ConfirmDeactivations);
    }

    [Fact]
    public void An_exported_value_goes_as_an_excel_number_or_date_unless_the_cell_cannot_keep_it()
    {
        var culture = CultureInfo.GetCultureInfo("es-AR");
        var profile = new CultureProfile(Spanish, culture, culture.NumberFormat, ["es-AR"], ["es"]);

        var whole = FormCatalogExportBuilder.ValueCell("1500", FormCatalogValueType.WholeNumber, profile);
        var fraction = FormCatalogExportBuilder.ValueCell("2500.25", FormCatalogValueType.DecimalNumber, profile);
        var long16 = FormCatalogExportBuilder.ValueCell("1234567890123456", FormCatalogValueType.WholeNumber, profile);
        var date = FormCatalogExportBuilder.ValueCell("2026-03-31", FormCatalogValueType.Date, profile);
        var old = FormCatalogExportBuilder.ValueCell("1899-12-31", FormCatalogValueType.Date, profile);

        Assert.Equal(((decimal?)1500m, "#,##0"), (whole.Number, whole.Format));
        Assert.Equal(((decimal?)2500.25m, "#,##0.00"), (fraction.Number, fraction.Format));
        Assert.Equal(("1234567890123456", (decimal?)null), (long16.Text, long16.Number));
        Assert.Equal(((DateOnly?)new DateOnly(2026, 3, 31), "dd/mm/yyyy"), (date.Date, date.Format));
        Assert.Equal(("1899-12-31", (DateOnly?)null), (old.Text, old.Date));
    }

    /// <summary>
    /// E13: una columna de un campo que no vino conserva lo guardado y una vacía lo limpia; solo lo que cambia va al delta, y el
    /// cambio dice el antes y el después como se leen, en la columna con el nombre del campo.
    /// </summary>
    [Fact]
    public void A_field_column_that_did_not_come_keeps_its_values_and_an_empty_one_clears_them()
    {
        var cuit = Field("cuit", "CUIT", FormCatalogFieldType.Text, 0);
        var plazo = Field("plazo_de_pago", "Plazo de pago", FormCatalogFieldType.WholeNumber, 1);
        var saved = new[] { Saved("A", "Acme", 0, fields: """{"cuit":"30-1","plazo_de_pago":"30"}""") };
        var fields = Fields([cuit, plazo]);

        var plan = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Append,
            FieldFile([plazo], Row(2, "A", "Acme", null, (plazo, FormCatalogFileFieldCell.Blank))), saved, 1, fields);
        var same = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Append,
            FieldFile([cuit, plazo], Row(2, "A", "Acme", null, (cuit, Typed(cuit, "30-1")), (plazo, Typed(plazo, "30")))), saved, 1, fields);

        var write = Assert.Single(plan.Writes);
        Assert.Equal([plazo.Id], write.Fields.Keys);
        Assert.Null(write.Fields[plazo.Id]);
        Assert.Equal([new FormCatalogImportChangedRow(2, "A", "Plazo de pago", "30", null)], plan.Lists.Changed);
        Assert.Equal((1, 0), (same.Unchanged, same.Writes.Count));
    }

    /// <summary>
    /// E13: una fuente se nombra por la identidad, el código o el texto de una opción activa; un texto que tienen dos activas no
    /// nombra a ninguna, y su código sí. Una opción dada de baja no se elige, pero la guardada se conserva aunque la celda la nombre
    /// por su texto (lección 1); si nombra una activa, pasa a esa. Al exportar, el texto que llevaría a otra va como su código.
    /// </summary>
    [Fact]
    public void A_source_option_is_named_by_its_identity_code_or_unique_text_and_the_stored_deactivated_one_is_kept()
    {
        var north = SourceOption("N", "Norte", isActive: false);
        var south = SourceOption("S", "Sur");
        var centerOne = SourceOption("C1", "Centro");
        var centerTwo = SourceOption("C2", "Centro");
        var zona = Field("zona", "Zona", FormCatalogFieldType.Catalog, 0, source: Guid.CreateVersion7());
        var options = new FormCatalogFieldOptions(new Dictionary<Guid, IReadOnlyList<FormCatalogStoredItemRow>>
        {
            [zona.SourceCatalogId!.Value] = [north, south, centerOne, centerTwo],
        });
        var stored = "{\"zona\":\"" + north.Id.ToString("D") + "\"}";
        var saved = new[] { Saved("A", "Acme", 0, fields: stored), Saved("B", "Beta", 1, fields: stored) };
        var file = FieldFile([zona],
            Row(2, "A", "Acme", null, (zona, new FormCatalogFileFieldCell("norte", null))),
            Row(3, "B", "Beta", null, (zona, new FormCatalogFileFieldCell("Sur", null))),
            Row(4, "N1", "Uno", null, (zona, new FormCatalogFileFieldCell(south.Id.ToString("D"), null))),
            Row(5, "N2", "Dos", null, (zona, new FormCatalogFileFieldCell("c2", null))),
            Row(6, "N3", "Tres", null, (zona, new FormCatalogFileFieldCell("Centro", null))),
            Row(7, "N4", "Cuatro", null, (zona, new FormCatalogFileFieldCell("Norte", null))));

        var plan = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Append, file, saved, 2,
            new FormCatalogImportFields([zona], options, (field, value) => options.TextOf(field.SourceCatalogId, Guid.Parse(value.GetString()!))));

        Assert.Equal(1, plan.Unchanged);
        Assert.Equal([new FormCatalogImportChangedRow(3, "B", "Zona", "Norte", "Sur")], plan.Lists.Changed);
        Assert.Equal([south.Id.ToString("D"), south.Id.ToString("D"), centerTwo.Id.ToString("D")],
            plan.Writes.Select(write => write.Fields[zona.Id]!.Value.GetString()));
        Assert.Equal([new FormCatalogImportErrorRow(6, "Zona", FormCatalogErrors.FieldValueInvalidCode, null, null),
            new FormCatalogImportErrorRow(7, "Zona", FormCatalogErrors.FieldValueInvalidCode, null, null)], plan.Lists.Errors);
        Assert.Equal(("Norte", "Sur", "C1"), (options.ExportText(zona.SourceCatalogId, north.Id),
            options.ExportText(zona.SourceCatalogId, south.Id), options.ExportText(zona.SourceCatalogId, centerOne.Id)));
    }

    /// <summary>
    /// E13: un campo obligatorio necesita su valor en una opción activa, también nueva sin su columna, y un valor único no puede
    /// repetirse ni en el archivo ni en las activas que quedan al agregar; al reemplazar, las que no vinieron se dan de baja y no
    /// cuentan. Un valor ambiguo dice lo que se escribió, como el valor de la E12.
    /// </summary>
    [Fact]
    public void A_required_field_needs_its_value_and_a_unique_one_cannot_repeat_in_the_file_or_in_the_options_that_stay()
    {
        var cuit = Field("cuit", "CUIT", FormCatalogFieldType.Text, 0, unique: true);
        var plazo = Field("plazo", "Plazo", FormCatalogFieldType.DecimalNumber, 1, required: true);
        var saved = new[]
        {
            Saved("A", "Acme", 0, fields: """{"cuit":"30-1","plazo":"30"}"""), Saved("K", "Kept", 1, fields: """{"cuit":"30-9","plazo":"5"}"""),
        };

        var missing = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Append, FieldFile([cuit],
            Row(2, "A", "Acme", null, (cuit, Typed(cuit, "30-2"))), Row(3, "N1", "Uno", null, (cuit, Typed(cuit, "30-3")))), saved, 2,
            Fields([cuit, plazo]));
        var repeated = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Append, FieldFile([cuit, plazo],
            Row(2, "N1", "Uno", null, (cuit, Typed(cuit, "30-3")), (plazo, Typed(plazo, "1"))),
            Row(3, "N2", "Dos", null, (cuit, Typed(cuit, "30-3")), (plazo, Typed(plazo, "2"))),
            Row(4, "N3", "Tres", null, (cuit, Typed(cuit, "30-9")), (plazo, Typed(plazo, "3")))), saved, 2, Fields([cuit, plazo]));
        var replaced = FormCatalogImportPolicy.Decide(FormCatalogValueType.Text, FormCatalogImportMode.Replace, FieldFile([cuit, plazo],
            Row(2, "N3", "Tres", null, (cuit, Typed(cuit, "30-9")), (plazo, Typed(plazo, "3"))),
            Row(3, "N4", "Cuatro", null, (cuit, Typed(cuit, "30-4")),
                (plazo, new FormCatalogFileFieldCell("1.500", FormCatalogFieldValue.Failed(FormCatalogFieldValueIssue.Ambiguous))))),
            saved, 2, Fields([cuit, plazo]));

        Assert.Equal([new FormCatalogImportErrorRow(3, "Plazo", FormCatalogErrors.FieldValueRequiredCode, null, null)], missing.Lists.Errors);
        Assert.Equal([new FormCatalogImportChangedRow(2, "A", "CUIT", "30-1", "30-2")], missing.Lists.Changed);
        Assert.Equal([
            new FormCatalogImportErrorRow(3, "CUIT", FormCatalogErrors.FieldValueDuplicateCode, null, 2),
            new FormCatalogImportErrorRow(4, "CUIT", FormCatalogErrors.FieldValueDuplicateCode, null, null),
        ], repeated.Lists.Errors);
        // Al reemplazar, «K» no vino y se da de baja: su CUIT queda libre.
        Assert.Equal([new FormCatalogImportErrorRow(3, "Plazo", FormCatalogImportErrors.ValueAmbiguousCode, "1.500", null)], replaced.Lists.Errors);
        Assert.Equal(["N3"], replaced.Lists.Added.Select(row => row.Code));
    }

    private static FormCatalogField Field(string key, string name, FormCatalogFieldType type, int position, bool required = false,
        bool unique = false, Guid? source = null) =>
        new(Guid.CreateVersion7(), key, name, type, source, required, unique, null, null, null, position, true);

    private static FormCatalogImportFields Fields(IReadOnlyList<FormCatalogField> fields) =>
        new(fields, FormCatalogFieldOptions.None, (_, value) => value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText());

    private static FormCatalogFileFieldCell Typed(FormCatalogField field, string canonical) =>
        new(canonical, field.Check(JsonSerializer.SerializeToElement(canonical)));

    private static FormCatalogStoredItemRow SourceOption(string code, string text, bool isActive = true) =>
        new(Guid.CreateVersion7(), code, FormCatalogItem.NormalizeCode(code), text, null, null, 0, isActive);

    private static FormCatalogFileRow Row(int row, string? code, string? text, string? detail,
        params (FormCatalogField Field, FormCatalogFileFieldCell Cell)[] cells) =>
        new(row, code, text, detail, null, cells.ToDictionary(cell => cell.Field.Id, cell => cell.Cell));

    private static FormCatalogFile FieldFile(IReadOnlyList<FormCatalogField> columns, params FormCatalogFileRow[] rows) =>
        new("hash", null, true, true, false, false, rows, [.. columns.Select(field => field.Id)]);

    private static FormCatalogStoredItemRow Saved(string code, string text, int position, string? detail = null, bool isActive = true,
        string fields = "{}") =>
        new(Guid.CreateVersion7(), code, FormCatalogItem.NormalizeCode(code), text, detail, null, position, isActive, fields);

    private static FormCatalogFileRow Row(int row, string? code, string? text, string? detail) => new(row, code, text, detail, null);

    private static FormCatalogFile File(bool hasDetail, params FormCatalogFileRow[] rows) =>
        new("hash", null, true, true, hasDetail, false, rows);

    private static CultureCatalogEntry Culture(string code, string datePattern, string decimalSeparator, string groupSeparator) =>
        new(code, code[..2], code[3..], datePattern, "HH:mm", datePattern + " HH:mm", datePattern, "a. m.", "p. m.", decimalSeparator,
            groupSeparator, "¤ #,##0.00", "#,##0 %", null, true, code == "es-AR", null, []);
}
