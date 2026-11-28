using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// El bloqueo del catálogo (C4): dos personas que suben y bajan opciones del mismo catálogo a la vez esperan las dos su lock; la
/// primera guarda y la segunda, con la versión vieja, recibe el 409 común, sin cruzar posiciones ni chocar con la restricción
/// diferida (un 500). Desde la E13, guardar bloquea también las fuentes de los campos, en la misma llamada y el mismo orden.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormCatalogConcurrencyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_people_moving_options_at_once_wait_for_the_catalog_lock_and_the_late_one_gets_409()
    {
        var (first, tenantId, _) = await FormsTestData.BusinessAsync(factory, "Subir y bajar");
        using var owner = first;
        using var second = await FormsTestData.MemberAsync(factory, tenantId, Permissions.Forms.CatalogsManage);
        var catalog = await FormsTestData.CreateManualAsync(factory, tenantId, "Orden", ("A", "Uno"), ("B", "Dos"), ("C", "Tres"));
        var path = $"{FormsTestData.Catalogs}/{catalog.Id:D}";
        var middle = (await OptionsAsync(owner, path)).Single(option => option.Code == "B").Id;

        var key = AdvisoryLockKeys.For(tenantId, "forms-catalog", catalog.Id);
        var (connection, transaction) = await FormsTestData.HoldLockAsync(factory, key);
        Task<HttpResponseMessage> up, down;
        await using (connection)
        await using (transaction)
        {
            up = owner.PutAsJsonAsync(path, Save(catalog.Version, middle, "Up"), Ct);
            down = second.PutAsJsonAsync(path, Save(catalog.Version, middle, "Down"), Ct);
            await FormsTestData.WaitForLockWaitersAsync(factory, key, 2);
            Assert.False(up.IsCompleted || down.IsCompleted, "Ninguno guarda mientras otro tiene el catálogo.");
            await transaction.CommitAsync(Ct);
        }

        using var upResponse = await up;
        using var downResponse = await down;
        var statuses = new[] { upResponse.StatusCode, downResponse.StatusCode };
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], statuses.Order());
        var late = upResponse.StatusCode == HttpStatusCode.Conflict ? upResponse : downResponse;
        using (var problem = JsonDocument.Parse(await late.Content.ReadAsStringAsync(Ct)))
            Assert.Equal("General.ConcurrencyConflict", problem.RootElement.GetProperty("code").GetString());

        // Solo el movimiento que guardó: B sube a la primera o baja a la última, y las posiciones siguen seguidas.
        string[] expected = upResponse.StatusCode == HttpStatusCode.OK ? ["B", "A", "C"] : ["A", "C", "B"];
        Assert.Equal(expected, (await OptionsAsync(owner, path)).Select(option => option.Code));
    }

    /// <summary>
    /// Un catálogo y su fuente (E13): guardar un valor de «De un catálogo» espera el bloqueo de la fuente, así lee sus opciones
    /// después de que otra persona las cambia (acá, dar de baja la opción elegida, que deja de servir como valor nuevo). Dos
    /// catálogos que son fuente uno del otro, guardados a la vez, toman los dos bloqueos en el mismo orden: los dos guardan, sin
    /// esperarse en cruz.
    /// </summary>
    [Fact]
    public async Task Saving_a_value_waits_for_its_source_and_two_catalogs_that_source_each_other_never_deadlock()
    {
        var (first, tenantId, _) = await FormsTestData.BusinessAsync(factory, "Catálogo y fuente");
        using var owner = first;
        using var second = await FormsTestData.MemberAsync(factory, tenantId, Permissions.Forms.CatalogsManage);
        var zones = await FormsTestData.CreateManualAsync(factory, tenantId, "Zonas", ("N", "Norte"), ("S", "Sur"));
        var zonesPath = $"{FormsTestData.Catalogs}/{zones.Id:D}";
        var zoneOptions = await OptionsAsync(owner, zonesPath);
        var catalog = Value(await RunAsync(tenantId, service => service.CreateAsync(new CreateFormCatalogRequest("Proveedores", null,
            FormCatalogSource.Manual, null, [new FormCatalogItemInput(null, "p", "P1", "Uno", null, null, true)], null, null, null,
            [new FormCatalogFieldInput(null, "zona", null, "Zona", FormCatalogFieldType.Catalog, zones.Id, false, false, null, null, null)]),
            Ct))).Catalog;
        var path = $"{FormsTestData.Catalogs}/{catalog.Id:D}";
        var option = (await OptionsAsync(owner, path))[0].Id;
        var zona = catalog.Fields[0].Id;
        var zonesKey = AdvisoryLockKeys.For(tenantId, "forms-catalog", zones.Id);

        // Otra persona tiene la fuente y da de baja Norte: el guardado espera y, al leer, Norte ya no sirve.
        var (connection, transaction) = await FormsTestData.HoldLockAsync(factory, zonesKey);
        Task<HttpResponseMessage> waiting;
        await using (connection)
        await using (transaction)
        {
            waiting = owner.PutAsJsonAsync(path, SaveValue("Proveedores", catalog.Version, option, "P1", "Uno", zona, zoneOptions[0].Id), Ct);
            await FormsTestData.WaitForLockWaitersAsync(factory, zonesKey, 1);
            Assert.False(waiting.IsCompleted, "El guardado espera el bloqueo de su fuente.");
            await using (var deactivate = new NpgsqlCommand("""
                SELECT set_config('app.tenant_id', @tenant, true);
                UPDATE tenant."FormCatalogItems" SET "IsActive" = false WHERE "Id" = @id;
                """, connection, transaction))
            {
                deactivate.Parameters.AddWithValue("tenant", tenantId.ToString());
                deactivate.Parameters.AddWithValue("id", zoneOptions[0].Id);
                await deactivate.ExecuteNonQueryAsync(Ct);
            }

            await transaction.CommitAsync(Ct);
        }

        using (var rejected = await waiting)
        {
            var body = await rejected.Content.ReadAsStringAsync(Ct);
            Assert.True(rejected.StatusCode == HttpStatusCode.BadRequest, body);
            using var problem = JsonDocument.Parse(body);
            Assert.Equal("Forms.Catalog.FieldValueInvalid", problem.RootElement.GetProperty("code").GetString());
        }

        // Zonas suma un campo que toma sus valores de Proveedores: cada uno es fuente del otro.
        var zonesVersion = Value(await RunAsync(tenantId, service => service.UpdateAsync(new UpdateFormCatalogRequest(zones.Id, "Zonas",
            null, true, zones.Version, null, null, null, null, null, null, null,
            [new FormCatalogFieldInput(null, "proveedor", null, "Proveedor", FormCatalogFieldType.Catalog, catalog.Id, false, false, null,
                null, null)]), Ct))).Version;
        var proveedor = Value(await RunAsync(tenantId, service => service.GetAsync(zones.Id, Ct))).Fields[0].Id;
        var catalogKey = AdvisoryLockKeys.For(tenantId, "forms-catalog", catalog.Id);
        string[] keys = [.. new[] { catalogKey, zonesKey }.Order(StringComparer.Ordinal)];

        (connection, transaction) = await FormsTestData.HoldLockAsync(factory, keys[0]);
        Task<HttpResponseMessage> saveCatalog, saveZones;
        await using (connection)
        await using (transaction)
        {
            await using (var lockSecond = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", connection, transaction))
            {
                lockSecond.Parameters.AddWithValue("key", keys[1]);
                await lockSecond.ExecuteNonQueryAsync(Ct);
            }

            saveCatalog = owner.PutAsJsonAsync(path, SaveValue("Proveedores", catalog.Version, option, "P1", "Uno", zona, zoneOptions[1].Id), Ct);
            saveZones = second.PutAsJsonAsync(zonesPath,
                SaveValue("Zonas", zonesVersion, zoneOptions[1].Id, zoneOptions[1].Code, "Sur", proveedor, option), Ct);
            await FormsTestData.WaitForLockWaitersAsync(factory, keys[0], 2);
            Assert.False(saveCatalog.IsCompleted || saveZones.IsCompleted, "Ninguno guarda mientras otro tiene los dos catálogos.");
            await transaction.CommitAsync(Ct);
        }

        using var catalogResponse = await saveCatalog;
        using var zonesResponse = await saveZones;
        Assert.True(catalogResponse.StatusCode == HttpStatusCode.OK, await catalogResponse.Content.ReadAsStringAsync(Ct));
        Assert.True(zonesResponse.StatusCode == HttpStatusCode.OK, await zonesResponse.Content.ReadAsStringAsync(Ct));
    }

    private static object SaveValue(string name, uint version, Guid itemId, string code, string text, Guid fieldId, Guid value) => new
    {
        name, isActive = true, version,
        items = new[] { new { itemId, code, text, isActive = true, fields = new Dictionary<string, string> { [fieldId.ToString()] = value.ToString() } } },
    };

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private async Task<T> RunAsync<T>(Guid tenantId, Func<IFormCatalogService, Task<T>> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await run(services.GetRequiredService<IFormCatalogService>());
    }

    private static object Save(uint version, Guid itemId, string direction) =>
        new { name = "Orden", isActive = true, version, moves = new[] { new { itemId, direction } } };

    // Las opciones por posición: el id y su código.
    private static async Task<(Guid Id, string Code)[]> OptionsAsync(HttpClient client, string path)
    {
        using var listed = await client.GetFromJsonAsync<JsonDocument>(path + "/items?pageSize=100", Ct);
        return [.. listed!.RootElement.GetProperty("items").EnumerateArray()
            .OrderBy(item => item.GetProperty("position").GetInt32())
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("code").GetString()!))];
    }
}
