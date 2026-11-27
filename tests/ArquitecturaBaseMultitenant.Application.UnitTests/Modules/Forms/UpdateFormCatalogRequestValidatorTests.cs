using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// Las reglas del guardado que no necesitan la base: el orden no se combina con movimientos ni con borrados, una opción por borrar
/// va una sola vez, cada opción lleva lo suyo y los ids no se repiten.
/// </summary>
public sealed class UpdateFormCatalogRequestValidatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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

    [Fact]
    public async Task An_item_id_or_a_new_local_id_goes_once_and_empty_local_ids_are_not_repeated()
    {
        var saved = Guid.NewGuid();
        var failure = await ValidateAsync(Request() with
        {
            Items =
            [
                new FormCatalogItemInput(saved, null, "A", "Uno", null, null, true),
                new FormCatalogItemInput(saved, "n1", "B", "Dos", null, null, true),
                // Su id local es el de una guardada, que no cuenta: es la única nueva con «n1».
                new FormCatalogItemInput(null, "n1", "C", "Tres", null, null, true),
                new FormCatalogItemInput(Guid.NewGuid(), "n2", "D", "Cuatro", null, null, true),
                new FormCatalogItemInput(null, "n2", "E", "Cinco", null, null, true),
                // Sin id local falta el dato, pero no se repite.
                new FormCatalogItemInput(null, "", "F", "Seis", null, null, true),
                new FormCatalogItemInput(null, null, "G", "Siete", null, null, true),
            ],
        });

        Assert.NotNull(failure);
        Assert.Equal(["items.0", "items.1", "items.5.localId", "items.6.localId"], failure.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([FormCatalogInputRules.Repeated], failure.Errors["items.0"]);
        Assert.Equal([FormCatalogInputRules.Repeated], failure.Errors["items.1"]);
    }

    private static UpdateFormCatalogRequest Request() =>
        new(Guid.NewGuid(), "Talles", null, true, 7, null, null, null, null, null, null, null);

    private static Task<Domain.Results.ValidationError?> ValidateAsync(UpdateFormCatalogRequest request) =>
        RequestValidator.ValidateAsync(request, [new UpdateFormCatalogRequestValidator()], Ct);
}
