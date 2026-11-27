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
/// el código y cierra el hueco; un movimiento fuera de rango revierte todo el guardado.
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
