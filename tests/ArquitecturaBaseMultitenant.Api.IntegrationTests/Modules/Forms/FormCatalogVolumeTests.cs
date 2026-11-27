using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// Volumen (E12, §11): un catálogo de 10.000 opciones. Listar, buscar, guardar una opción y exportar no traen todo para quedarse
/// con poco: cada lectura de las opciones, con el recorrido secuencial apagado, usa un índice y devuelve a lo sumo una tanda
/// (EXPLAIN ANALYZE, como <c>BrowserSessionAuthorizationTests</c>), y guardar una opción escribe y audita solo esa y sube la
/// versión del catálogo.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormCatalogVolumeTests(ApiFactory factory)
{
    private const string Catalogs = FormsTestData.Catalogs;
    private const int Options = FormCatalogLimits.MaxItems;
    private const int LargestRead = 1_000;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Listing_searching_saving_one_option_and_exporting_ten_thousand_never_read_them_all()
    {
        var capture = new ItemCommandCapture();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(capture))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Volumen " + Guid.NewGuid().ToString("N")[..8], Ct);
        await FormsTestData.EnableAsync(factory, tenantId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        var catalog = await FormsTestData.CreateManualAsync(factory, tenantId, "Diez mil",
            [.. Enumerable.Range(1, Options).Select(index => ($"C{index:D5}", $"Opción {index:D5}"))]);
        // Las estadísticas de la tabla, como las deja el autovacuum después de una carga grande.
        await using (var connection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var analyze = new NpgsqlCommand("""ANALYZE tenant."FormCatalogItems" """, connection);
            await analyze.ExecuteNonQueryAsync(Ct);
        }

        // Listar: la primera página y la última.
        capture.Clear();
        var first = await ReadAsync(client, $"{Catalogs}/{catalog.Id}/items?status=All");
        Assert.Equal(Options, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(10, first.GetProperty("items").GetArrayLength());
        var last = await ReadAsync(client, $"{Catalogs}/{catalog.Id}/items?status=All&page={Options / 100}&pageSize=100");
        Assert.Equal($"C{Options:D5}", last.GetProperty("items")[99].GetProperty("code").GetString());
        await AssertBoundedAsync(tenantId, capture.Reads(), "listar");

        // Buscar: una sola, sin traer las demás.
        capture.Clear();
        var found = await ReadAsync(client, $"{Catalogs}/{catalog.Id}/items?status=All&search=09876");
        Assert.Equal(1, found.GetProperty("totalCount").GetInt32());
        await AssertBoundedAsync(tenantId, capture.Reads(), "buscar");

        // Guardar una opción: escribe y audita solo esa, y sube la versión del catálogo.
        var itemId = found.GetProperty("items")[0].GetProperty("id").GetGuid();
        var auditsBefore = await ItemAuditsAsync(tenantId, catalog.Id);
        capture.Clear();
        using (var saved = await client.PutAsJsonAsync($"{Catalogs}/{catalog.Id}", new
        {
            name = catalog.Name, isActive = true, version = catalog.Version,
            items = new[] { new { itemId, code = "C09876", text = "Opción cambiada", isActive = true } },
        }, Ct))
        {
            var body = await saved.Content.ReadAsStringAsync(Ct);
            Assert.True(saved.StatusCode == HttpStatusCode.OK, body);
            Assert.NotEqual(catalog.Version, JsonDocument.Parse(body).RootElement.GetProperty("version").GetUInt32());
        }
        Assert.Single(capture.Writes(), command => command.Sql.Contains("UPDATE tenant.\"FormCatalogItems\"", StringComparison.Ordinal));
        Assert.Equal(auditsBefore + 1, await ItemAuditsAsync(tenantId, catalog.Id));
        await AssertBoundedAsync(tenantId, capture.Reads(), "guardar una opción");

        // Exportar: todas, de a tandas.
        capture.Clear();
        using (var exported = await client.GetAsync($"{Catalogs}/{catalog.Id}/export", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
            Assert.True((await exported.Content.ReadAsByteArrayAsync(Ct)).Length > 0);
        }
        Assert.True(capture.Reads().Count >= Options / LargestRead, "Exportar lee las opciones de a tandas.");
        await AssertBoundedAsync(tenantId, capture.Reads(), "exportar");
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>
    /// Cada lectura de las opciones, con el recorrido secuencial apagado y la organización en el alcance: sin <c>Seq Scan</c> sobre
    /// la tabla y con a lo sumo una tanda de filas.
    /// </summary>
    private async Task AssertBoundedAsync(Guid tenantId, IReadOnlyList<CapturedCommand> reads, string step)
    {
        Assert.NotEmpty(reads);
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using (var setup = new NpgsqlCommand("SET enable_seqscan = off; SELECT set_config('app.tenant_id', @tenant, false)", connection))
        {
            setup.Parameters.AddWithValue("tenant", tenantId.ToString("D"));
            await setup.ExecuteNonQueryAsync(Ct);
        }
        foreach (var read in reads)
        {
            await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, FORMAT JSON) " + read.Sql, connection);
            foreach (var (name, value) in read.Parameters) explain.Parameters.AddWithValue(name, value);
            var plan = (string)(await explain.ExecuteScalarAsync(Ct))!;
            using var document = JsonDocument.Parse(plan);
            var root = document.RootElement[0].GetProperty("Plan");
            Assert.False(SequentialOnItems(root), $"{step}: recorre toda la tabla de opciones\n{read.Sql}");
            Assert.True(root.GetProperty("Actual Rows").GetDouble() <= LargestRead, $"{step}: trae más de una tanda\n{read.Sql}");
        }
    }

    private static bool SequentialOnItems(JsonElement node) =>
        node.GetProperty("Node Type").GetString() == "Seq Scan" && node.TryGetProperty("Relation Name", out var relation)
            && relation.GetString() == "FormCatalogItems"
        || node.TryGetProperty("Plans", out var children) && children.EnumerateArray().Any(SequentialOnItems);

    private async Task<long> ItemAuditsAsync(Guid tenantId, Guid catalogId)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT set_config('app.tenant_id', @tenant, false);
            SELECT count(*) FROM tenant."AuditEntries" audit JOIN tenant."FormCatalogItems" item ON audit."EntityId" = item."Id"
            WHERE item."CatalogId" = @catalog
            """, connection);
        command.Parameters.AddWithValue("tenant", tenantId.ToString("D"));
        command.Parameters.AddWithValue("catalog", catalogId);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        await reader.NextResultAsync(Ct);
        await reader.ReadAsync(Ct);
        return reader.GetInt64(0);
    }

    private sealed record CapturedCommand(string Sql, (string Name, object Value)[] Parameters);

    /// <summary>Guarda los comandos que tocan las opciones: las lecturas para su plan y las escrituras para contarlas.</summary>
    private sealed class ItemCommandCapture : DbCommandInterceptor
    {
        private readonly List<CapturedCommand> _commands = [];

        internal void Clear()
        {
            lock (_commands) _commands.Clear();
        }

        internal IReadOnlyList<CapturedCommand> Reads()
        {
            lock (_commands)
                return [.. _commands.Where(command => command.Sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)
                    && !command.Sql.TrimEnd().TrimEnd(';').Contains(';', StringComparison.Ordinal))];
        }

        internal IReadOnlyList<CapturedCommand> Writes()
        {
            lock (_commands)
                return [.. _commands.Where(command => !command.Sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal))
                    .SelectMany(command => command.Sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(statement => command with { Sql = statement }))];
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        private void Capture(DbCommand command)
        {
            if (!command.CommandText.Contains("\"FormCatalogItems\"", StringComparison.Ordinal)) return;
            lock (_commands)
                _commands.Add(new(command.CommandText, [.. command.Parameters.Cast<DbParameter>()
                    .Select(parameter => (parameter.ParameterName, parameter.Value ?? DBNull.Value))]));
        }
    }
}
