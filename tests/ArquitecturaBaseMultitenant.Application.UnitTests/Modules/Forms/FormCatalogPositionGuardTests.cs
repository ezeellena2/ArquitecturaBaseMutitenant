using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// «Ordenar por…» (224) y las reglas del guardado que no necesitan la base: el orden es estable, lo vacío va al final en los dos
/// sentidos y los números dentro de un texto se comparan por su valor; el orden no se combina con movimientos ni con borrados.
/// </summary>
public sealed class FormCatalogPositionGuardTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-AR");

    [Fact]
    public void Sorting_by_text_is_stable_keeps_empty_last_both_ways_and_compares_numbers_by_value()
    {
        (string? Text, int Was)[] rows =
            [("Talle 10030", 0), ("", 1), ("banana", 2), ("Talle 117", 3), ("Banana", 4), (null, 5), ("Ananá", 6)];

        var ascending = FormCatalogPositionGuard.SortByText(rows, row => row.Text, descending: false, Spanish).Select(row => row.Was);
        // «Banana» y «banana» empatan sin distinguir mayúsculas y conservan su orden; «117» va antes que «10030».
        Assert.Equal([6, 2, 4, 3, 0, 1, 5], ascending);

        var descending = FormCatalogPositionGuard.SortByText(rows, row => row.Text, descending: true, Spanish).Select(row => row.Was);
        Assert.Equal([0, 3, 2, 4, 6, 1, 5], descending);
    }

    [Fact]
    public async Task Sort_with_moves_or_deletions_is_rejected_in_sort()
    {
        var id = Guid.NewGuid();
        var sort = new FormCatalogItemSortInput(FormCatalogItemSortColumn.Text, Descending: false);

        var withMoves = await ValidateAsync(Request() with { Sort = sort, Moves = [new FormCatalogMoveInput(id, FormCatalogMoveDirection.Up)] });
        Assert.NotNull(withMoves);
        Assert.Equal(["sort"], withMoves.Errors.Keys);
        var withDeletions = await ValidateAsync(Request() with { Sort = sort, DeleteItems = [id] });
        Assert.NotNull(withDeletions);
        Assert.Equal(["sort"], withDeletions.Errors.Keys);
        Assert.Null(await ValidateAsync(Request() with { Sort = sort }));
    }

    [Fact]
    public async Task A_deletion_goes_once_and_never_also_in_items_or_moves_and_is_active_is_required()
    {
        var edited = Guid.NewGuid();
        var moved = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        var failure = await ValidateAsync(Request() with
        {
            IsActive = null,
            Items = [new FormCatalogItemInput(edited, null, "A", "Uno", null, null, true)],
            Moves = [new FormCatalogMoveInput(moved, FormCatalogMoveDirection.Down)],
            DeleteItems = [deleted, deleted, edited, moved],
        });

        Assert.NotNull(failure);
        Assert.Equal(["deleteItems.0", "deleteItems.1", "deleteItems.2", "deleteItems.3", "isActive"], failure.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Items_need_code_text_state_and_a_local_id_when_new_and_the_code_fits_its_limit()
    {
        var failure = await ValidateAsync(Request() with
        {
            Items =
            [
                new FormCatalogItemInput(null, null, new string('c', 41), "", null, null, null),
                new FormCatalogItemInput(null, "n1", "B", "Dos", null, null, true),
                new FormCatalogItemInput(null, "n1", "C", "Tres", null, null, true),
            ],
        });

        Assert.NotNull(failure);
        Assert.Equal(["items.0.code", "items.0.isActive", "items.0.localId", "items.0.text", "items.1", "items.2"],
            failure.Errors.Keys.Order(StringComparer.Ordinal));
    }

    private static UpdateFormCatalogRequest Request() =>
        new(Guid.NewGuid(), "Talles", null, true, 7, null, null, null, null, null, null, null);

    private static Task<Domain.Results.ValidationError?> ValidateAsync(UpdateFormCatalogRequest request) =>
        RequestValidator.ValidateAsync(request, [new UpdateFormCatalogRequestValidator()], Ct);
}
