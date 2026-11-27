using UniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence.UniqueKeys;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using FormCatalogFilters = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogFilters;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// La persistencia de los catálogos (E12, §3): las tres tablas privadas con la RLS forzada y el GRANT, las restricciones diferidas
/// del código y la posición (C4), los CHECK y el lector, con cada orden del listado en los dos sentidos.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormCatalogPersistenceTests(ApiFactory factory)
{
    private static readonly string[] Tables = ["FormCatalogs", "FormCatalogItems", "FormWorkLeases"];

    [Fact]
    public async Task Each_table_forces_rls_grants_the_runtime_role_and_hides_other_organizations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var admin = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await admin.OpenAsync(ct);
            foreach (var table in Tables)
            {
                await using var command = new NpgsqlCommand("""
                    SELECT c.relrowsecurity AND c.relforcerowsecurity
                        AND EXISTS (SELECT 1 FROM pg_policies p WHERE p.schemaname = 'tenant' AND p.tablename = c.relname AND p.policyname = 'tenant_scope')
                    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'tenant' AND c.relname = @table
                    """, admin);
                command.Parameters.AddWithValue("table", table);
                Assert.True((bool)(await command.ExecuteScalarAsync(ct))!, table);
            }
        }

        var (tenantId, _) = await OrganizationAsync(ct);
        var catalog = await SaveManualAsync(tenantId, FormCatalogValueType.Text, [("A", "Uno", null)], ct);
        await using var runtime = new NpgsqlConnection(factory.RuntimeConnectionString);
        await runtime.OpenAsync(ct);
        foreach (var table in Tables)
        {
            await using var grants = new NpgsqlCommand($"""
                SELECT has_table_privilege(current_user, 'tenant."{table}"', 'SELECT') AND has_table_privilege(current_user, 'tenant."{table}"', 'INSERT')
                    AND has_table_privilege(current_user, 'tenant."{table}"', 'UPDATE') AND has_table_privilege(current_user, 'tenant."{table}"', 'DELETE')
                """, runtime);
            Assert.True((bool)(await grants.ExecuteScalarAsync(ct))!, table);
        }

        // Sin organización, la app no ve nada; otra organización tampoco lo encuentra.
        await using (var hidden = new NpgsqlCommand("""SELECT count(*) FROM tenant."FormCatalogs" WHERE "Id" = @id""", runtime))
        {
            hidden.Parameters.AddWithValue("id", catalog.Id);
            Assert.Equal(0L, await hidden.ExecuteScalarAsync(ct));
        }
        var (otherTenant, _) = await OrganizationAsync(ct);
        Assert.Null(await ReadAsync(otherTenant, reader => reader.FindAsync(catalog.Id, ct)));
        Assert.NotNull(await ReadAsync(tenantId, reader => reader.FindAsync(catalog.Id, ct)));
    }

    [Fact]
    public async Task Deferred_keys_let_a_save_swap_codes_and_positions_and_reject_a_repeated_code_at_commit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (tenantId, _) = await OrganizationAsync(ct);
        var catalog = await SaveManualAsync(tenantId, FormCatalogValueType.Text, [("A", "Uno", null), ("B", "Dos", null)], ct);

        // Un guardado intercambia códigos y posiciones: a mitad de camino se repiten, y al confirmar ya no.
        var swapped = await WriteAsync(tenantId, async (repository, ctx) =>
        {
            await repository.LockAsync([catalog.Id], ctx);
            var locked = await repository.GetByIdAsync(catalog.Id, catalog.Version, ctx);
            Assert.NotNull(locked);
            var items = await repository.ListAllItemsAsync(catalog.Id, ctx);
            Assert.True(items[0].Edit(locked, 0, "B", items[0].Text, null, null).IsSuccess);
            Assert.True(items[1].Edit(locked, 1, "A", items[1].Text, null, null).IsSuccess);
            Assert.True(items[0].SwapPosition(items[1]).IsSuccess);
            repository.MarkUpdated(locked);
            return Result.Success();
        }, ct);
        Assert.True(swapped.IsSuccess);
        var rows = await ReadAsync(tenantId, reader => reader.ListItemsAsync(catalog.Id, new ListFormCatalogItemsRequest(), ct));
        Assert.Equal(["A", "B"], rows.Items.Select(row => row.Code));
        Assert.Equal(["Dos", "Uno"], rows.Items.Select(row => row.Text));
        var current = await ReadAsync(tenantId, reader => reader.FindAsync(catalog.Id, ct));
        Assert.NotEqual(catalog.Version, current!.Version);

        // Un código repetido sin distinguir mayúsculas choca al confirmar con su restricción. Es el respaldo: el servicio lo valida
        // antes, bajo el bloqueo del catálogo, y un choque al confirmar sería un 500 (C4), por eso la UoW no lo traduce.
        var repeated = await Assert.ThrowsAsync<PostgresException>(() => WriteAsync(tenantId, async (repository, ctx) =>
        {
            await repository.LockAsync([catalog.Id], ctx);
            var locked = await repository.GetByIdAsync(catalog.Id, current.Version, ctx);
            repository.Add(FormCatalogItem.Create(locked!, 0, "a", "Tres", null, null, true, 2).Value);
            return Result.Success();
        }, ct));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, repeated.SqlState);
        Assert.Equal(UniqueKeys.FormCatalogItemCode, repeated.ConstraintName);

        var position = await Assert.ThrowsAsync<PostgresException>(() => WriteAsync(tenantId, async (repository, ctx) =>
        {
            await repository.LockAsync([catalog.Id], ctx);
            var locked = await repository.GetByIdAsync(catalog.Id, current.Version, ctx);
            repository.Add(FormCatalogItem.Create(locked!, 0, "C", "Tres", null, null, true, 1).Value);
            return Result.Success();
        }, ct));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, position.SqlState);
        Assert.Equal(UniqueKeys.FormCatalogItemPosition, position.ConstraintName);
    }

    [Fact]
    public async Task Checks_tie_the_filters_to_the_source_and_keep_a_single_value()
    {
        var ct = TestContext.Current.CancellationToken;
        var (tenantId, _) = await OrganizationAsync(ct);
        var catalog = await SaveManualAsync(tenantId, FormCatalogValueType.Text, [("A", "Uno", null)], ct);
        await using var admin = new NpgsqlConnection(factory.AdminConnectionString);
        await admin.OpenAsync(ct);
        await using (var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", admin))
        {
            enter.Parameters.AddWithValue("tenant", tenantId.ToString());
            await enter.ExecuteNonQueryAsync(ct);
        }

        async Task<string?> ViolatedAsync(string sql)
        {
            await using var command = new NpgsqlCommand(sql, admin);
            command.Parameters.AddWithValue("id", catalog.Id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(ct));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            return error.ConstraintName;
        }

        Assert.Equal("CK_FormCatalogs_Filters_Source", await ViolatedAsync("""UPDATE tenant."FormCatalogs" SET "Filters" = '{}' WHERE "Id" = @id"""));
        Assert.Equal("CK_FormCatalogs_ValueType_Source", await ViolatedAsync("""UPDATE tenant."FormCatalogs" SET "Source" = 'Members', "Filters" = '{}', "ValueType" = 'Date' WHERE "Id" = @id"""));
        Assert.Equal("CK_FormCatalogItems_SingleValue", await ViolatedAsync("""UPDATE tenant."FormCatalogItems" SET "NumberValue" = 1, "DateValue" = '2026-01-01' WHERE "CatalogId" = @id"""));
    }

    [Fact]
    public async Task The_reader_sorts_every_column_both_ways_counts_options_and_flags_repeated_texts()
    {
        var ct = TestContext.Current.CancellationToken;
        var (tenantId, _) = await OrganizationAsync(ct);
        var manual = await SaveManualAsync(tenantId, FormCatalogValueType.DecimalNumber,
            [("A", "Rojo", "1500.25"), ("B", "rojo", "2"), ("C", "Verde", "0.0000000001")], ct);
        var empty = await SaveManualAsync(tenantId, FormCatalogValueType.Text, [], ct);
        var members = await SaveAsync(tenantId, FormCatalog.CreateMembers("Miembros", null,
            new FormCatalogFilters(1, [], true, null, null, null)), ct);
        var inactive = FormCatalog.CreateManual("Viejo", null, FormCatalogValueType.Text);
        inactive.Deactivate();
        await SaveAsync(tenantId, inactive, ct);

        var page = await ReadAsync(tenantId, reader => reader.ListAsync(new ListFormCatalogsRequest { Sort = "optionsCount" }, ct));
        Assert.Equal(4, page.TotalCount);
        // Los dos sin opciones empatan en cero y desempatan por el id; el de miembros no tiene cuenta y queda al final.
        Assert.Equal([empty.Id, inactive.Id, manual.Id, members.Id], page.Items.Select(row => row.Id));
        page = await ReadAsync(tenantId, reader => reader.ListAsync(new ListFormCatalogsRequest { Sort = "status" }, ct));
        Assert.Equal(inactive.Id, page.Items[^1].Id);
        Assert.Equal(empty.Id, page.Items[^2].Id);
        foreach (var field in ListFormCatalogsRequest.SortableFields)
        {
            foreach (var sort in new[] { field, "-" + field })
            {
                var sorted = await ReadAsync(tenantId, reader => reader.ListAsync(new ListFormCatalogsRequest { Sort = sort }, ct));
                Assert.Equal(4, sorted.Items.Count);
            }
        }

        var filtered = await ReadAsync(tenantId, reader => reader.ListAsync(new ListFormCatalogsRequest
        {
            Source = FormCatalogSourceFilter.Manual, Status = FormCatalogStatusFilter.Active, Search = "catalogo",
        }, ct));
        Assert.Equal(new[] { empty.Id, manual.Id }.Order(), filtered.Items.Select(row => row.Id).Order());
        var summary = Assert.Single(filtered.Items, row => row.Id == manual.Id);
        Assert.Equal((3, 3), (summary.TotalItems, summary.ActiveItems));
        Assert.Equal(new FormCatalogFilterCountsResponse(4, 3), await ReadAsync(tenantId, reader => reader.CountFiltersAsync(ct)));
        Assert.NotNull((await ReadAsync(tenantId, reader => reader.FindAsync(members.Id, ct)))!.Filters);

        var items = await ReadAsync(tenantId, reader => reader.ListItemsAsync(manual.Id, new ListFormCatalogItemsRequest(), ct));
        Assert.Equal(["1500.25", "2", "0.0000000001"], items.Items.Select(row => row.Value));
        Assert.Equal([true, true, false], items.Items.Select(row => row.TextRepeated));
        var searched = await ReadAsync(tenantId, reader => reader.ListItemsAsync(manual.Id, new ListFormCatalogItemsRequest { Search = "c" }, ct));
        Assert.Equal(["C"], searched.Items.Select(row => row.Code));
    }

    [Fact]
    public async Task The_reader_finds_the_candidates_of_every_search_at_once_without_accents_or_wildcards()
    {
        var ct = TestContext.Current.CancellationToken;
        var (tenantId, _) = await OrganizationAsync(ct);
        var manual = await SaveManualAsync(tenantId, FormCatalogValueType.Text,
            [("AZU", "Azul", null), ("AZM", "Azul marino", null), ("NAN", "Ñandú", null), ("P50", "50% menos", null), ("X21", "Otra", null),
                ("X_1", "Equis", null)], ct);
        await SaveManualAsync(tenantId, FormCatalogValueType.Text, [("AZO", "Azul de otro catálogo", null)], ct);

        // Una por búsqueda, la primera por posición; los comodines se buscan como texto y «nada» no trae ninguna.
        var first = await ReadAsync(tenantId, reader => reader.ListItemCandidatesAsync(manual.Id, ["azul", "nandu", "%", "x_", "nada"], 1, ct));
        Assert.Equal(new Dictionary<string, string> { ["azul"] = "AZU", ["nandu"] = "NAN", ["%"] = "P50", ["x_"] = "X_1" },
            first.ToDictionary(row => row.Search, row => row.Code));
        // Todas las de una búsqueda, por posición, y solo de ese catálogo.
        var all = await ReadAsync(tenantId, reader => reader.ListItemCandidatesAsync(manual.Id, ["AZUL"], 10, ct));
        Assert.Equal(["AZU", "AZM"], all.Select(row => row.Code));
    }

    private async Task<(Guid TenantId, AccountJourney Owner)> OrganizationAsync(CancellationToken ct)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Catálogos " + Guid.NewGuid().ToString("N")[..8], ct);
        return (tenantId, owner);
    }

    private async Task<FormCatalogStoredRow> SaveManualAsync(Guid tenantId, FormCatalogValueType valueType,
        IReadOnlyList<(string Code, string Text, string? Value)> items, CancellationToken ct)
    {
        var catalog = FormCatalog.CreateManual("Catálogo " + Guid.NewGuid().ToString("N")[..6], null, valueType);
        var saved = await WriteAsync(tenantId, (repository, _) =>
        {
            repository.Add(catalog);
            for (var index = 0; index < items.Count; index++)
            {
                var (code, text, value) = items[index];
                repository.Add(FormCatalogItem.Create(catalog, index, code, text, null, value, true, index).Value);
            }
            return Task.FromResult(Result.Success());
        }, ct);
        Assert.True(saved.IsSuccess);
        return (await ReadAsync(tenantId, reader => reader.FindAsync(catalog.Id, ct)))!;
    }

    private async Task<FormCatalogStoredRow> SaveAsync(Guid tenantId, FormCatalog catalog, CancellationToken ct)
    {
        var saved = await WriteAsync(tenantId, (repository, _) =>
        {
            repository.Add(catalog);
            return Task.FromResult(Result.Success());
        }, ct);
        Assert.True(saved.IsSuccess);
        return (await ReadAsync(tenantId, reader => reader.FindAsync(catalog.Id, ct)))!;
    }

    private async Task<Result> WriteAsync(Guid tenantId, Func<IFormCatalogRepository, CancellationToken, Task<Result>> work,
        CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var repository = services.GetRequiredService<IFormCatalogRepository>();
        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            transactionCt => work(repository, transactionCt), CommitPolicy.OnSuccess, ct);
    }

    private async Task<T> ReadAsync<T>(Guid tenantId, Func<IFormCatalogReader, Task<T>> read)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await read(services.GetRequiredService<IFormCatalogReader>());
    }
}
