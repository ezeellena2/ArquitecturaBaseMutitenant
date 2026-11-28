using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// Guardar a mano (E12, C3, C4 y C6) por el servicio, con la base real: el listado ordena por cada columna con lo vacío al final
/// (264); guardar sin cambios compara la versión sin tocar ni auditar opciones; «Ordenar por…» (224) ordena todas; borrar libera
/// el código y cierra el hueco; un movimiento fuera de rango revierte todo el guardado. Desde la E13, los campos, la presentación y
/// los valores de las opciones: el delta, guardar sin cambios, ordenar por un campo y una opción entre 10.000; cambiar el tipo o
/// una regla de un campo, que comprueba todas las opciones y revierte todo si una no entra; quitar un campo con valores, que se
/// confirma y los conserva; y lo que protege a una fuente: <c>SourceInUse</c> e <c>ItemInUse</c> con <c>uses: options</c>.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormCatalogServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task The_list_sorts_by_every_column_both_ways_with_the_empty_count_last_and_enums_by_their_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var manual = await CreateManualAsync(tenantId, "Talles", [("S", "Chico"), ("M", "Mediano")], ct);
        var empty = await CreateManualAsync(tenantId, "Vacío", [], ct);
        var members = Value(await RunAsync(tenantId, service => service.CreateAsync(new CreateFormCatalogRequest("Equipo", null,
            FormCatalogSource.Members, null, null, new FormCatalogFiltersInput(1, [], true, null, null), null, null), ct))).Catalog;
        var inactive = await CreateManualAsync(tenantId, "Viejo", [("A", "Uno")], ct);
        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(inactive) with { IsActive = false }, ct)));

        async Task<Guid[]> SortedAsync(string sort) => [.. Value(await RunAsync(tenantId, service =>
            service.ListAsync(new ListFormCatalogsRequest { Sort = sort }, ct))).Items.Select(row => row.Id)];

        // El de miembros no tiene cuenta y queda al final en los dos sentidos.
        Assert.Equal([empty.Id, inactive.Id, manual.Id, members.Id], await SortedAsync("optionsCount"));
        Assert.Equal([manual.Id, inactive.Id, empty.Id, members.Id], await SortedAsync("-optionsCount"));
        // El origen y el estado van por su orden, no por su texto: activo, sin opciones activas y dado de baja.
        Assert.Equal(members.Id, (await SortedAsync("source"))[^1]);
        Assert.Equal(members.Id, (await SortedAsync("-source"))[0]);
        Assert.Equal([empty.Id, inactive.Id], (await SortedAsync("status"))[^2..]);
        Assert.Equal([inactive.Id, empty.Id], (await SortedAsync("-status"))[..2]);
        Assert.Equal([inactive.Id, empty.Id, manual.Id, members.Id], await SortedAsync("-name"));
        foreach (var field in ListFormCatalogsRequest.SortableFields) Assert.Equal(4, (await SortedAsync(field)).Length);

        var rows = Value(await RunAsync(tenantId, service => service.ListAsync(new ListFormCatalogsRequest(), ct))).Items;
        Assert.Equal(Expected(2, true, null), Row(rows, manual.Id));
        Assert.Equal(Expected(0, false, FormCatalogUnusableReason.NoActiveOptions), Row(rows, empty.Id));
        Assert.Equal(Expected(1, false, null), Row(rows, inactive.Id));
        Assert.Null(rows.Single(row => row.Id == members.Id).OptionsCount);
        Assert.Equal(new FormCatalogFilterCountsResponse(4, 3), Value(await RunAsync(tenantId, service => service.GetFilterCountsAsync(ct))));
        Assert.NotNull(Value(await RunAsync(tenantId, service => service.GetAsync(members.Id, ct))).Filters);
    }

    [Fact]
    public async Task Saving_without_changes_compares_the_version_and_neither_touches_nor_audits_the_options()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var catalog = await CreateManualAsync(tenantId, "Colores", [("R", "Rojo"), ("V", "Verde")], ct);
        var items = await ItemsAsync(tenantId, catalog.Id, ct);
        var auditsBefore = await CountItemAuditsAsync(tenantId, items.Select(item => item.Id), ct);

        var saved = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog), ct)));
        Assert.NotEqual(catalog.Version, saved.Version);
        Assert.Empty(saved.ItemIdentityMap);
        Assert.Equal(auditsBefore, await CountItemAuditsAsync(tenantId, items.Select(item => item.Id), ct));
        Assert.Equal(0L, await CountModifiedItemsAsync(tenantId, catalog.Id, ct));

        // La versión vieja da el 409 común, aunque no cambie nada.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync(tenantId, service => service.UpdateAsync(Update(catalog), ct)));
        var again = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with { Version = saved.Version }, ct)));
        Assert.NotEqual(saved.Version, again.Version);
    }

    [Fact]
    public async Task Saving_the_same_filters_does_not_audit_a_change_of_filters()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var filters = new FormCatalogFiltersInput(1, [], true, null, null);
        var members = Value(await RunAsync(tenantId, service => service.CreateAsync(new CreateFormCatalogRequest("Equipo", null,
            FormCatalogSource.Members, null, null, filters, null, null), ct))).Catalog;
        var auditsBefore = await CountItemAuditsAsync(tenantId, [members.Id], ct);
        Assert.True(auditsBefore > 0);

        // La base guarda el jsonb con otro orden y otros espacios: los mismos filtros no son un cambio.
        var saved = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(members) with { Filters = filters }, ct)));

        Assert.NotEqual(members.Version, saved.Version);
        Assert.Equal(auditsBefore, await CountItemAuditsAsync(tenantId, [members.Id], ct));
    }

    [Fact]
    public async Task An_old_version_is_the_common_conflict_before_any_rule_of_the_options()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var catalog = await CreateManualAsync(tenantId, "Frutas", [("A", "Uno"), ("B", "Dos")], ct);
        var items = await ItemsAsync(tenantId, catalog.Id, ct);

        // Otra persona borra la opción A y guarda.
        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with { DeleteItems = [items[0].Id] }, ct)));

        // Con la ficha vieja, editar A, borrarla o pasar el tope da el 409 común, no ItemNotFound ni otra regla.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items = [new FormCatalogItemInput(items[0].Id, null, "A", "Uno editado", null, null, true)],
        }, ct)));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            DeleteItems = [items[0].Id],
        }, ct)));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Moves = [new FormCatalogMoveInput(items[1].Id, FormCatalogMoveDirection.Up)],
        }, ct)));
    }

    [Fact]
    public async Task Sorting_reorders_every_option_also_other_pages_and_inactive_ones_keeping_ties_and_numbers_by_value()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var options = Enumerable.Range(1, 22).Select(number => ($"C{number:00}", $"Relleno {number:00}")).ToList();
        options.Insert(3, ("T2", "Talle 117"));
        options.Insert(0, ("T1", "Talle 10030"));
        options.Add(("T3", "talle 117"));
        var catalog = await CreateManualAsync(tenantId, "Grande", options, ct);
        var all = await ItemsAsync(tenantId, catalog.Id, ct);
        var deactivated = all.Single(item => item.Code == "T2");
        var current = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items = [new FormCatalogItemInput(deactivated.Id, null, deactivated.Code, deactivated.Text, null, null, false)],
        }, ct)));

        var sorted = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = current.Version, Sort = new FormCatalogItemSortInput(FormCatalogItemSortColumn.Text, Descending: true),
        }, ct)));

        var after = await ItemsAsync(tenantId, catalog.Id, ct);
        // De la Z a la A, con los números por su valor: «10030» va antes que «117»; «Talle 117» (dada de baja) y «talle 117»
        // empatan y conservan su orden.
        Assert.Equal(["T1", "T2", "T3"], after.Take(3).Select(item => item.Code));
        Assert.Equal("C22", after[3].Code);
        Assert.Equal("C01", after[^1].Code);
        Assert.Equal(Enumerable.Range(0, after.Count), after.Select(item => item.Position));
        Assert.False(after[1].IsActive);

        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = sorted.Version, Sort = new FormCatalogItemSortInput(FormCatalogItemSortColumn.Code, Descending: false),
        }, ct)));
        Assert.Equal(["T1", "T2", "T3"], (await ItemsAsync(tenantId, catalog.Id, ct)).TakeLast(3).Select(item => item.Code));

        var combined = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Sort = new FormCatalogItemSortInput(FormCatalogItemSortColumn.Text, false), DeleteItems = [deactivated.Id],
        }, ct));
        Assert.Equal(["sort"], Assert.IsType<ValidationError>(combined.Error).Errors.Keys);
    }

    [Fact]
    public async Task Deleting_frees_the_code_closes_the_gap_and_a_move_out_of_range_reverts_the_whole_save()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var catalog = await CreateManualAsync(tenantId, "Sabores", [("A", "Uno"), ("B", "Dos"), ("C", "Tres")], ct);
        var items = await ItemsAsync(tenantId, catalog.Id, ct);

        var saved = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items = [new FormCatalogItemInput(null, "nuevo", "a", "Otro uno", null, null, true)], DeleteItems = [items[0].Id],
        }, ct)));
        var created = Assert.Single(saved.ItemIdentityMap);
        Assert.Equal("nuevo", created.LocalId);
        var after = await ItemsAsync(tenantId, catalog.Id, ct);
        Assert.Equal([("B", 0), ("C", 1), ("a", 2)], after.Select(item => (item.Code, item.Position)));
        Assert.Equal(created.ItemId, after[2].Id);

        // Un código repetido es un error de validación con el número de la opción que lo tiene, nunca un 500 en el commit.
        var repeated = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = saved.Version, Items = [new FormCatalogItemInput(after[0].Id, null, "C", "Dos", null, null, true)],
        }, ct));
        Assert.Equal(FormCatalogErrors.DuplicateCodeCode, repeated.Error.Code);
        Assert.Equal(2, repeated.Error.Metadata!["n"]);
        // Dos opciones intercambian sus códigos en un mismo guardado.
        var swapped = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = saved.Version,
            Items =
            [
                new FormCatalogItemInput(after[0].Id, null, "C", "Dos", null, null, true),
                new FormCatalogItemInput(after[1].Id, null, "B", "Tres", null, null, true),
            ],
        }, ct)));

        var moved = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = swapped.Version,
            Moves = [new FormCatalogMoveInput(after[2].Id, FormCatalogMoveDirection.Up), new FormCatalogMoveInput(after[0].Id, FormCatalogMoveDirection.Up)],
        }, ct));
        Assert.Equal(FormCatalogErrors.MoveOutOfRange, moved.Error);
        Assert.Equal(["C", "B", "a"], (await ItemsAsync(tenantId, catalog.Id, ct)).Select(item => item.Code));
        var other = await CreateManualAsync(tenantId, "Otro", [("Z", "Zeta")], ct);
        var foreignItem = (await ItemsAsync(tenantId, other.Id, ct))[0].Id;
        var foreign = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = swapped.Version, DeleteItems = [foreignItem],
        }, ct));
        Assert.Equal(["deleteItems.0"], Assert.IsType<ValidationError>(foreign.Error).Errors.Keys);
        Assert.Equal(FormCatalogErrors.ItemNotFoundCode, foreign.Error.Code);
    }

    /// <summary>
    /// Los valores de los campos (E13): el delta guarda por la identidad del campo, ausente conserva y <c>null</c> limpia; un texto
    /// vacío se guarda y se relee (lección 3); el detalle trae los campos con su fuente y cuántas opciones tienen valor, la
    /// presentación y el mapa de los campos nuevos; un valor único no se repite, y uno nuevo de una fuente pide una opción activa,
    /// mientras el guardado que apunta a una dada de baja se conserva (lección 1).
    /// </summary>
    [Fact]
    public async Task Field_values_are_a_delta_where_absent_keeps_and_null_clears_and_the_detail_counts_them()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var zones = await CreateManualAsync(tenantId, "Zonas", [("N", "Norte"), ("S", "Sur")], ct);
        var north = (await ItemsAsync(tenantId, zones.Id, ct))[0];
        var catalog = await CreateWithFieldsAsync(tenantId, "Proveedores", [("P1", "Uno"), ("P2", "Dos")],
            [NewField("cuit", "CUIT", FormCatalogFieldType.Text, unique: true), NewField("plazo", "Plazo", FormCatalogFieldType.WholeNumber),
                NewField("zona", "Zona", FormCatalogFieldType.Catalog, zones.Id)], ct);
        Assert.Equal(["cuit", "plazo", "zona"], catalog.Fields.Select(field => field.Key));
        Assert.Equal("Zonas", catalog.Fields[2].SourceCatalogName);
        var (cuit, plazo, zona) = (catalog.Fields[0].Id, catalog.Fields[1].Id, catalog.Fields[2].Id);
        var items = await ItemsAsync(tenantId, catalog.Id, ct);

        var first = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items =
            [
                Edit(items[0], new() { [cuit] = Json("20-123"), [plazo] = Json("30"), [zona] = Json(north.Id.ToString()) }),
                Edit(items[1], new() { [cuit] = Json("") }),
            ],
        }, ct)));
        var values = (await ItemsAsync(tenantId, catalog.Id, ct)).Select(item => item.Fields).ToList();
        Assert.Equal("30", values[0][plazo].GetString());
        Assert.Equal(north.Id.ToString("D"), values[0][zona].GetString());
        Assert.Equal("", values[1][cuit].GetString());

        // Solo el plazo, en null: lo demás queda.
        var second = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = first.Version, Items = [Edit(items[0], new() { [plazo] = null })],
        }, ct)));
        var kept = (await ItemsAsync(tenantId, catalog.Id, ct))[0].Fields;
        Assert.Equal([cuit, zona], kept.Keys.Order());
        Assert.Equal("20-123", kept[cuit].GetString());
        var detail = Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct)));
        Assert.Equal([1, 0, 1], detail.Fields.Select(field => field.ValuesCount));

        // Un valor único, con otras mayúsculas y espacios, ya lo tiene la opción 1.
        var repeated = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = second.Version, Items = [Edit(items[1], new() { [cuit] = Json(" 20-123 ") })],
        }, ct));
        Assert.Equal(FormCatalogErrors.FieldValueDuplicateCode, repeated.Error.Code);
        Assert.Equal([$"items.0.fields.{cuit}"], Assert.IsType<ValidationError>(repeated.Error).Errors.Keys);

        // Dar de baja Norte no frena guardar lo que la tiene, pero una opción nueva que la nombra no sirve.
        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(zones) with
        {
            Items = [new FormCatalogItemInput(north.Id, null, north.Code, north.Text, null, null, false)],
        }, ct)));
        var third = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = second.Version, Items = [Edit(items[0] with { Text = "Uno editado" }, null)],
        }, ct)));
        Assert.Equal(north.Id.ToString("D"), (await ItemsAsync(tenantId, catalog.Id, ct))[0].Fields[zona].GetString());
        var inactive = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = third.Version, Items = [Edit(items[1], new() { [zona] = Json(north.Id.ToString()) })],
        }, ct));
        Assert.Equal([$"items.0.fields.{zona}"], Assert.IsType<ValidationError>(inactive.Error).Errors.Keys);
        Assert.Equal(FormCatalogErrors.FieldValueInvalidCode, inactive.Error.Code);

        // Un campo nuevo vuelve con su identidad, y la presentación queda como se pidió.
        var withNew = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = third.Version,
            Fields = [.. detail.Fields.Select(Keep), NewField("nuevo", "Contacto", FormCatalogFieldType.Text)],
            Presentation = new FormCatalogPresentationInput(["text", "field:" + cuit], ["detail"]),
        }, ct)));
        var created = Assert.Single(withNew.FieldIdentityMap);
        Assert.Equal("nuevo", created.LocalId);
        var after = Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct)));
        Assert.Equal(created.FieldId, after.Fields[3].Id);
        Assert.Equal(["text", "field:" + cuit.ToString("D")], after.Presentation.Title);
    }

    /// <summary>
    /// Guardar los mismos campos, la misma presentación y los mismos valores compara la versión sin tocar ni auditar opciones; con
    /// una versión vieja, el 409 común antes de cualquier regla de los campos.
    /// </summary>
    [Fact]
    public async Task Saving_the_same_fields_and_values_neither_touches_nor_audits_options_and_an_old_version_is_the_common_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var catalog = await CreateWithFieldsAsync(tenantId, "Clientes", [("A", "Uno"), ("B", "Dos")],
            [NewField("plazo", "Plazo", FormCatalogFieldType.DecimalNumber)], ct);
        var plazo = catalog.Fields[0].Id;
        var items = await ItemsAsync(tenantId, catalog.Id, ct);
        var saved = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items = [Edit(items[0], new() { [plazo] = Json("1.50") })],
        }, ct)));
        var auditsBefore = await CountItemAuditsAsync(tenantId, items.Select(item => item.Id), ct);
        var modifiedBefore = await CountModifiedItemsAsync(tenantId, catalog.Id, ct);

        var again = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = saved.Version,
            Fields = [.. catalog.Fields.Select(Keep)],
            Presentation = new FormCatalogPresentationInput(["text"], ["detail"]),
            Items = [Edit(items[0], new() { [plazo] = Json("1.5") })],
        }, ct)));

        Assert.NotEqual(saved.Version, again.Version);
        Assert.Empty(again.FieldIdentityMap);
        Assert.Equal(auditsBefore, await CountItemAuditsAsync(tenantId, items.Select(item => item.Id), ct));
        Assert.Equal(modifiedBefore, await CountModifiedItemsAsync(tenantId, catalog.Id, ct));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = saved.Version, Fields = [NewField("otro", "Otro", FormCatalogFieldType.Date)],
            Items = [Edit(items[1], new() { [plazo] = Json("no es un número") })],
        }, ct)));
    }

    /// <summary>
    /// «Ordenar por…» un campo (224): cada tipo compara en el suyo (un número, una fecha, sí o no con «No» primero, el texto de la
    /// opción de otro catálogo y un texto), con lo vacío al final en los dos sentidos; un campo que no está activo da 400 en
    /// <c>sort.column</c>.
    /// </summary>
    [Fact]
    public async Task Sorting_by_a_field_of_each_type_compares_in_its_type_with_the_empty_ones_last_both_ways()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var zones = await CreateManualAsync(tenantId, "Zonas", [("S", "Sur"), ("C", "Centro"), ("N", "Norte")], ct);
        var zone = (await ItemsAsync(tenantId, zones.Id, ct)).ToDictionary(item => item.Code, item => item.Id.ToString());
        var catalog = await CreateWithFieldsAsync(tenantId, "Ordenar", [("A", "Uno"), ("B", "Dos"), ("C", "Tres"), ("D", "Cuatro")],
            [NewField("numero", "Número", FormCatalogFieldType.DecimalNumber), NewField("fecha", "Fecha", FormCatalogFieldType.Date),
                NewField("activo", "Activo", FormCatalogFieldType.YesNo), NewField("zona", "Zona", FormCatalogFieldType.Catalog, zones.Id),
                NewField("nombre", "Nombre", FormCatalogFieldType.Text)], ct);
        var ids = catalog.Fields.ToDictionary(field => field.Key, field => field.Id);
        var items = await ItemsAsync(tenantId, catalog.Id, ct);
        var version = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items =
            [
                Edit(items[0], Values(ids, "10", "2026-03-01", true, zone["S"], "beta")),
                Edit(items[1], Values(ids, "9.5", "2025-12-31", false, zone["C"], "Alfa")),
                Edit(items[3], Values(ids, "100", "2026-01-15", null, zone["N"], "gamma")),
            ],
        }, ct))).Version;

        async Task<string[]> SortAsync(string key, bool descending)
        {
            version = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
            {
                Version = version, Sort = new FormCatalogItemSortInput(ids[key].ToString(), descending),
            }, ct))).Version;
            return [.. (await ItemsAsync(tenantId, catalog.Id, ct)).Select(item => item.Code)];
        }

        Assert.Equal(["B", "A", "D", "C"], await SortAsync("numero", false));
        Assert.Equal(["D", "A", "B", "C"], await SortAsync("numero", true));
        Assert.Equal(["B", "D", "A", "C"], await SortAsync("fecha", false));
        Assert.Equal(["A", "D", "B", "C"], await SortAsync("fecha", true));
        Assert.Equal(["B", "A", "D", "C"], await SortAsync("activo", false));
        Assert.Equal(["A", "B", "D", "C"], await SortAsync("activo", true));
        Assert.Equal(["B", "D", "A", "C"], await SortAsync("zona", false));
        Assert.Equal(["A", "D", "B", "C"], await SortAsync("zona", true));
        Assert.Equal(["B", "A", "D", "C"], await SortAsync("nombre", false));
        Assert.Equal(["D", "A", "B", "C"], await SortAsync("nombre", true));

        var unknown = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = version, Sort = new FormCatalogItemSortInput(Guid.NewGuid().ToString(), false),
        }, ct));
        Assert.Equal(["sort.column"], Assert.IsType<ValidationError>(unknown.Error).Errors.Keys);
        var removed = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = version, Fields = [.. catalog.Fields.Skip(1).Select(Keep)],
            Sort = new FormCatalogItemSortInput(ids["numero"].ToString(), false),
        }, ct));
        Assert.Equal(["sort.column"], Assert.IsType<ValidationError>(removed.Error).Errors.Keys);
    }

    /// <summary>Un valor entre 10.000 opciones: escribe y audita solo esa, y el valor único se compara sin traerlas todas.</summary>
    [Fact]
    public async Task Saving_one_value_among_ten_thousand_options_writes_and_audits_only_that_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var catalog = await CreateWithFieldsAsync(tenantId, "Diez mil",
            [.. Enumerable.Range(1, FormCatalogLimits.MaxItems).Select(index => ($"C{index:D5}", $"Opción {index:D5}"))],
            [NewField("cuit", "CUIT", FormCatalogFieldType.Text, unique: true)], ct);
        var cuit = catalog.Fields[0].Id;
        var found = Value(await RunAsync(tenantId, service => service.ListItemsAsync(catalog.Id,
            new ListFormCatalogItemsRequest { Search = "09876", Status = FormCatalogStatusFilter.All }, ct))).Items.Single();
        var other = Value(await RunAsync(tenantId, service => service.ListItemsAsync(catalog.Id,
            new ListFormCatalogItemsRequest { Search = "00042", Status = FormCatalogStatusFilter.All }, ct))).Items.Single();
        var auditsBefore = await CountCatalogItemAuditsAsync(tenantId, catalog.Id, ct);

        var saved = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items = [Edit(found, new() { [cuit] = Json("30-7") })],
        }, ct)));

        Assert.Equal(1L, await CountModifiedItemsAsync(tenantId, catalog.Id, ct));
        Assert.Equal(auditsBefore + 1, await CountCatalogItemAuditsAsync(tenantId, catalog.Id, ct));
        var repeated = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = saved.Version, Items = [Edit(other, new() { [cuit] = Json("30-7") })],
        }, ct));
        Assert.Equal(FormCatalogErrors.FieldValueDuplicateCode, repeated.Error.Code);
    }

    /// <summary>
    /// Cambiar el tipo o una regla (B5) comprueba todas las opciones: lo que no convierte da sus filas y no cambia nada, tampoco el
    /// otro campo del mismo guardado; con el valor corregido en el mismo guardado convierte las demás, y a texto queda el de la opción
    /// fuente aunque esté dada de baja; único y obligatorio miran solo las opciones activas.
    /// </summary>
    [Fact]
    public async Task Changing_a_field_type_or_rule_checks_every_option_and_an_error_reverts_the_whole_save()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var zones = await CreateManualAsync(tenantId, "Zonas", [("N", "Norte"), ("S", "Sur")], ct);
        var north = (await ItemsAsync(tenantId, zones.Id, ct))[0];
        var catalog = await CreateWithFieldsAsync(tenantId, "Proveedores", [("P1", "Uno"), ("P2", "Dos"), ("P3", "Tres")],
            [NewField("plazo", "Plazo", FormCatalogFieldType.Text), NewField("zona", "Zona", FormCatalogFieldType.Catalog, zones.Id)], ct);
        var (plazo, zona) = (catalog.Fields[0].Id, catalog.Fields[1].Id);
        var items = await ItemsAsync(tenantId, catalog.Id, ct);
        var version = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items =
            [
                Edit(items[0], new() { [plazo] = Json("30"), [zona] = Json(north.Id.ToString()) }),
                Edit(items[1], new() { [plazo] = Json("treinta") }),
                Edit(items[2] with { IsActive = false }, new() { [plazo] = Json("30") }),
            ],
        }, ct))).Version;
        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(zones) with
        {
            Items = [new FormCatalogItemInput(north.Id, null, north.Code, north.Text, null, null, false)],
        }, ct)));
        var detail = Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct)));
        FormCatalogFieldInput Changed(int index, FormCatalogFieldType type, bool required = false, bool unique = false) =>
            Keep(detail.Fields[index]) with { Type = type, SourceCatalogId = null, Required = required, Unique = unique };

        var failed = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = version, Fields = [Changed(0, FormCatalogFieldType.WholeNumber), Changed(1, FormCatalogFieldType.Text)],
        }, ct));
        Assert.Equal(FormCatalogErrors.FieldTypeNotConvertibleCode, failed.Error.Code);
        Assert.Equal(["fields.0.type"], Assert.IsType<ValidationError>(failed.Error).Errors.Keys);
        Assert.Equal(1, failed.Error.Metadata!["invalidCount"]);
        var row = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<FormCatalogInvalidRow>>(failed.Error.Metadata["invalidRows"]));
        Assert.Equal((1, items[1].Id, "Dos", FormCatalogInvalidRow.Invalid), (row.Position, row.ItemId, row.Text, row.Reason));
        Assert.Equal([FormCatalogFieldType.Text, FormCatalogFieldType.Catalog],
            Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct))).Fields.Select(field => field.Type));
        Assert.Equal(north.Id.ToString("D"), (await ItemsAsync(tenantId, catalog.Id, ct))[0].Fields[zona].GetString());

        var converted = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = version, Fields = [Changed(0, FormCatalogFieldType.WholeNumber), Changed(1, FormCatalogFieldType.Text)],
            Items = [Edit(items[1], new() { [plazo] = Json("45") })],
        }, ct)));
        var values = (await ItemsAsync(tenantId, catalog.Id, ct)).Select(item => item.Fields).ToList();
        Assert.Equal(["30", "45", "30"], values.Select(value => value[plazo].GetString()));
        Assert.Equal("Norte", values[0][zona].GetString());

        detail = Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct)));
        var unique = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = converted.Version, Fields = [Changed(0, FormCatalogFieldType.WholeNumber, unique: true), Changed(1, FormCatalogFieldType.Text)],
        }, ct)));
        detail = Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct)));
        var missing = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = unique.Version,
            Fields = [Changed(0, FormCatalogFieldType.WholeNumber, unique: true), Changed(1, FormCatalogFieldType.Text, required: true)],
        }, ct));
        Assert.Equal(FormCatalogErrors.FieldValuesMissingCode, missing.Error.Code);
        Assert.Equal(["fields.1.required"], Assert.IsType<ValidationError>(missing.Error).Errors.Keys);
    }

    /// <summary>
    /// Quitar un campo con valores pide confirmarlo, con cuántos son, en el lugar que tenía entre los activos; uno sin valores se quita
    /// sin confirmar. Confirmado, el campo queda quitado con sus valores, sale de la presentación (el título vacío vuelve al texto) y
    /// no vuelve.
    /// </summary>
    [Fact]
    public async Task Removing_a_field_with_values_needs_its_confirmation_and_keeps_its_values()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var catalog = await CreateWithFieldsAsync(tenantId, "Proveedores", [("P1", "Uno"), ("P2", "Dos"), ("P3", "Tres")],
            [NewField("plazo", "Plazo", FormCatalogFieldType.WholeNumber), NewField("cuit", "CUIT", FormCatalogFieldType.Text),
                NewField("nota", "Nota", FormCatalogFieldType.Text)], ct);
        var (plazo, cuit, nota) = (catalog.Fields[0].Id, catalog.Fields[1].Id, catalog.Fields[2].Id);
        var items = await ItemsAsync(tenantId, catalog.Id, ct);
        var version = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items =
            [
                Edit(items[0], new() { [cuit] = Json("20-1"), [plazo] = Json("30") }),
                Edit(items[1] with { IsActive = false }, new() { [cuit] = Json("20-2") }),
            ],
            Presentation = new FormCatalogPresentationInput(["field:" + cuit], ["field:" + plazo, "detail"]),
        }, ct))).Version;
        var detail = Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct)));

        var unconfirmed = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = version, Fields = [Keep(detail.Fields[0]), Keep(detail.Fields[2])],
        }, ct));
        Assert.Equal(FormCatalogErrors.FieldRemovalNotConfirmedCode, unconfirmed.Error.Code);
        var errors = Assert.IsType<ValidationError>(unconfirmed.Error).Errors;
        Assert.Equal(["fields.1"], errors.Keys);
        Assert.Contains(" 2 ", Assert.Single(errors["fields.1"]), StringComparison.Ordinal);
        Assert.All(Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct))).Fields, field => Assert.True(field.IsActive));

        // La nota no tiene valores: no hace falta confirmarla.
        var removed = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = version, Fields = [Keep(detail.Fields[0])], ConfirmedFieldRemovals = [cuit],
        }, ct)));
        var after = Value(await RunAsync(tenantId, service => service.GetAsync(catalog.Id, ct)));
        Assert.Equal([(plazo, true, 1), (cuit, false, 2), (nota, false, 0)],
            after.Fields.Select(field => (field.Id, field.IsActive, field.ValuesCount)));
        Assert.Equal(["text"], after.Presentation.Title);
        Assert.Equal(["field:" + plazo.ToString("D"), "detail"], after.Presentation.Description);
        Assert.Equal([plazo], (await ItemsAsync(tenantId, catalog.Id, ct))[0].Fields.Keys);

        var back = await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = removed.Version, Fields = [Keep(after.Fields[0]), Keep(after.Fields[1])],
        }, ct));
        Assert.Equal(FormCatalogErrors.FieldInvalidCode, back.Error.Code);
    }

    /// <summary>
    /// Un catálogo que es la fuente de un campo activo de otro no se da de baja: es <c>SourceInUse</c>, un 409 que no es el de la
    /// versión (lección 2) y no cambia nada; quitado el campo, se da de baja. Uno ya dado de baja se sigue guardando.
    /// </summary>
    [Fact]
    public async Task A_catalog_that_is_the_source_of_an_active_field_is_not_deactivated_until_the_field_is_removed()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var zones = await CreateManualAsync(tenantId, "Zonas", [("N", "Norte")], ct);
        var catalog = await CreateWithFieldsAsync(tenantId, "Proveedores", [("P1", "Uno")],
            [NewField("zona", "Zona", FormCatalogFieldType.Catalog, zones.Id)], ct);

        var inUse = await RunAsync(tenantId, service => service.UpdateAsync(Update(zones) with { IsActive = false }, ct));
        Assert.Equal((FormCatalogErrors.SourceInUseCode, ErrorType.Conflict), (inUse.Error.Code, inUse.Error.Type));
        var stillActive = Value(await RunAsync(tenantId, service => service.GetAsync(zones.Id, ct)));
        Assert.Equal((true, zones.Version), (stillActive.IsActive, stillActive.Version));

        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with { Fields = [] }, ct)));
        var deactivated = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(zones) with { IsActive = false }, ct)));
        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(zones) with
        {
            Name = "Zonas viejas", IsActive = false, Version = deactivated.Version,
        }, ct)));
        Assert.False(Value(await RunAsync(tenantId, service => service.GetAsync(zones.Id, ct))).IsActive);
    }

    /// <summary>
    /// Borrar una opción que es el valor de un campo activo de otro catálogo no borra ninguna de las pedidas: <c>ItemInUse</c> en la
    /// que se usa, con <c>uses: options</c>. Darla de baja sí se puede, y quitado el campo, ya no la usa nadie.
    /// </summary>
    [Fact]
    public async Task Deleting_an_option_that_another_option_uses_deletes_none()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = await OrganizationAsync(ct);
        var zones = await CreateManualAsync(tenantId, "Zonas", [("N", "Norte"), ("S", "Sur"), ("O", "Oeste")], ct);
        var zoneItems = await ItemsAsync(tenantId, zones.Id, ct);
        var (north, south) = (zoneItems[0], zoneItems[1]);
        var catalog = await CreateWithFieldsAsync(tenantId, "Proveedores", [("P1", "Uno"), ("P2", "Dos")],
            [NewField("zona", "Zona", FormCatalogFieldType.Catalog, zones.Id)], ct);
        var zona = catalog.Fields[0].Id;
        var supplier = (await ItemsAsync(tenantId, catalog.Id, ct))[0];
        var version = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Items = [Edit(supplier with { IsActive = false }, new() { [zona] = Json(north.Id.ToString()) })],
        }, ct))).Version;

        var inUse = await RunAsync(tenantId, service => service.UpdateAsync(Update(zones) with { DeleteItems = [south.Id, north.Id] }, ct));
        Assert.Equal((FormCatalogErrors.ItemInUseCode, ErrorType.Conflict), (inUse.Error.Code, inUse.Error.Type));
        Assert.Equal(["deleteItems.1"], Assert.IsType<ValidationError>(inUse.Error).Errors.Keys);
        Assert.Equal([FormCatalogErrors.UsedByOptions], Assert.IsAssignableFrom<IReadOnlyList<string>>(inUse.Error.Metadata!["uses"]));
        Assert.Equal(3, (await ItemsAsync(tenantId, zones.Id, ct)).Count);

        var deactivated = Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(zones) with
        {
            Items = [new FormCatalogItemInput(north.Id, null, north.Code, north.Text, null, null, false)],
        }, ct)));
        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(catalog) with
        {
            Version = version, Fields = [], ConfirmedFieldRemovals = [zona],
        }, ct)));
        Value(await RunAsync(tenantId, service => service.UpdateAsync(Update(zones) with
        {
            Version = deactivated.Version, DeleteItems = [north.Id],
        }, ct)));
        Assert.Equal([south.Id, zoneItems[2].Id], (await ItemsAsync(tenantId, zones.Id, ct)).Select(item => item.Id));
    }

    private static FormCatalogFieldInput NewField(string localId, string name, FormCatalogFieldType type, Guid? source = null,
        bool unique = false) => new(null, localId, null, name, type, source, false, unique, null, null, null);

    private static FormCatalogFieldInput Keep(FormCatalogFieldDetail field) => new(field.Id, null, field.Key, field.Name, field.Type,
        field.SourceCatalogId, field.Required, field.Unique, field.MaxLength, field.Min, field.Max);

    private static FormCatalogItemInput Edit(FormCatalogItemRow item, Dictionary<Guid, JsonElement?>? fields) =>
        new(item.Id, null, item.Code, item.Text, item.Detail, item.Value, item.IsActive, fields);

    private static JsonElement? Json<T>(T value) => JsonSerializer.SerializeToElement(value);

    private static Dictionary<Guid, JsonElement?> Values(Dictionary<string, Guid> ids, string number, string date, bool? yes, string zone,
        string name) => new()
    {
        [ids["numero"]] = Json(number), [ids["fecha"]] = Json(date), [ids["activo"]] = yes is { } value ? Json(value) : null,
        [ids["zona"]] = Json(zone), [ids["nombre"]] = Json(name),
    };

    private async Task<FormCatalogResponse> CreateWithFieldsAsync(Guid tenantId, string name, List<(string Code, string Text)> items,
        List<FormCatalogFieldInput> fields, CancellationToken ct)
    {
        var request = new CreateFormCatalogRequest(name, null, FormCatalogSource.Manual, null,
            [.. items.Select((item, index) => new FormCatalogItemInput(null, $"n{index}", item.Code, item.Text, null, null, true))], null, null,
            null, fields);
        return Value(await RunAsync(tenantId, service => service.CreateAsync(request, ct))).Catalog;
    }

    private async Task<long> CountCatalogItemAuditsAsync(Guid tenantId, Guid catalogId, CancellationToken ct)
    {
        await using var admin = await AdminAsync(tenantId, ct);
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM tenant."AuditEntries" AS audit
            JOIN tenant."FormCatalogItems" AS item ON item."Id" = audit."EntityId"
            WHERE item."CatalogId" = @id
            """, admin);
        command.Parameters.AddWithValue("id", catalogId);
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    private static (int? Count, bool Usable, FormCatalogUnusableReason? Reason) Expected(int? count, bool usable,
        FormCatalogUnusableReason? reason) => (count, usable, reason);

    private static (int? Count, bool Usable, FormCatalogUnusableReason? Reason) Row(IEnumerable<FormCatalogRow> rows, Guid id)
    {
        var row = rows.Single(candidate => candidate.Id == id);
        return (row.OptionsCount, row.Usable, row.UnusableReason);
    }

    private static UpdateFormCatalogRequest Update(FormCatalogResponse catalog) =>
        new(catalog.Id, catalog.Name, catalog.Description, true, catalog.Version, null, null, null, null, null, null, null);

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private async Task<FormCatalogResponse> CreateManualAsync(Guid tenantId, string name, List<(string Code, string Text)> items,
        CancellationToken ct)
    {
        var request = new CreateFormCatalogRequest(name, null, FormCatalogSource.Manual, null,
            [.. items.Select((item, index) => new FormCatalogItemInput(null, $"n{index}", item.Code, item.Text, null, null, true))], null, null,
            null);
        var created = Value(await RunAsync(tenantId, service => service.CreateAsync(request, ct)));
        Assert.Equal(items.Count, created.ItemIdentityMap.Count);
        return created.Catalog;
    }

    private async Task<IReadOnlyList<FormCatalogItemRow>> ItemsAsync(Guid tenantId, Guid catalogId, CancellationToken ct)
    {
        var rows = new List<FormCatalogItemRow>();
        for (var page = 1; ; page++)
        {
            var current = Value(await RunAsync(tenantId, service => service.ListItemsAsync(catalogId,
                new ListFormCatalogItemsRequest { Page = page, PageSize = 10, Status = FormCatalogStatusFilter.All }, ct)));
            rows.AddRange(current.Items);
            if (rows.Count >= current.TotalCount) return rows;
        }
    }

    private async Task<long> CountItemAuditsAsync(Guid tenantId, IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        await using var admin = await AdminAsync(tenantId, ct);
        await using var command = new NpgsqlCommand("""SELECT count(*) FROM tenant."AuditEntries" WHERE "EntityId" = ANY(@ids)""", admin);
        command.Parameters.AddWithValue("ids", itemIds.ToArray());
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    private async Task<long> CountModifiedItemsAsync(Guid tenantId, Guid catalogId, CancellationToken ct)
    {
        await using var admin = await AdminAsync(tenantId, ct);
        await using var command = new NpgsqlCommand(
            """SELECT count(*) FROM tenant."FormCatalogItems" WHERE "CatalogId" = @id AND "ModifiedAtUtc" IS NOT NULL""", admin);
        command.Parameters.AddWithValue("id", catalogId);
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    private async Task<NpgsqlConnection> AdminAsync(Guid tenantId, CancellationToken ct)
    {
        var admin = new NpgsqlConnection(factory.AdminConnectionString);
        await admin.OpenAsync(ct);
        await using var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", admin);
        enter.Parameters.AddWithValue("tenant", tenantId.ToString());
        await enter.ExecuteNonQueryAsync(ct);
        return admin;
    }

    private async Task<Guid> OrganizationAsync(CancellationToken ct)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, ct);
        return await OrganizationJourney.StageAsync(factory, owner, "Catálogos " + Guid.NewGuid().ToString("N")[..8], ct);
    }

    private async Task<T> RunAsync<T>(Guid tenantId, Func<IFormCatalogService, Task<T>> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await run(services.GetRequiredService<IFormCatalogService>());
    }
}
