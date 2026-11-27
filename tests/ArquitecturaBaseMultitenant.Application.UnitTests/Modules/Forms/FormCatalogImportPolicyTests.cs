using System.Globalization;
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

    private static FormCatalogStoredItemRow Saved(string code, string text, int position, string? detail = null, bool isActive = true) =>
        new(Guid.CreateVersion7(), code, FormCatalogItem.NormalizeCode(code), text, detail, null, position, isActive);

    private static FormCatalogFileRow Row(int row, string? code, string? text, string? detail) => new(row, code, text, detail, null);

    private static FormCatalogFile File(bool hasDetail, params FormCatalogFileRow[] rows) =>
        new("hash", null, true, true, hasDetail, false, rows);

    private static CultureCatalogEntry Culture(string code, string datePattern, string decimalSeparator, string groupSeparator) =>
        new(code, code[..2], code[3..], datePattern, "HH:mm", datePattern + " HH:mm", datePattern, "a. m.", "p. m.", decimalSeparator,
            groupSeparator, "¤ #,##0.00", "#,##0 %", null, true, code == "es-AR", null, []);
}
