using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// «Ordenar por…» (224): el orden es estable, lo vacío va al final en los dos sentidos y los números dentro de un texto se comparan
/// por su valor.
/// </summary>
public sealed class FormCatalogPositionEditorTests
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-AR");

    [Fact]
    public void Sorting_by_text_is_stable_keeps_empty_last_both_ways_and_compares_numbers_by_value()
    {
        (string? Text, int Was)[] rows =
            [("Talle 10030", 0), ("", 1), ("banana", 2), ("Talle 117", 3), ("Banana", 4), (null, 5), ("Ananá", 6)];

        var ascending = FormCatalogPositionEditor.SortByText(rows, row => row.Text, descending: false, Spanish).Select(row => row.Was);
        // «Banana» y «banana» empatan sin distinguir mayúsculas y conservan su orden; «117» va antes que «10030».
        Assert.Equal([6, 2, 4, 3, 0, 1, 5], ascending);

        var descending = FormCatalogPositionEditor.SortByText(rows, row => row.Text, descending: true, Spanish).Select(row => row.Was);
        Assert.Equal([0, 3, 2, 4, 6, 1, 5], descending);
    }
}
