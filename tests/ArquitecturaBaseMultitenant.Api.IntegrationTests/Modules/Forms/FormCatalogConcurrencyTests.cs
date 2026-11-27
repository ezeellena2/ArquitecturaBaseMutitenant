using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// El bloqueo del catálogo (C4): dos personas que suben y bajan opciones del mismo catálogo a la vez esperan las dos su lock; la
/// primera guarda y la segunda, con la versión vieja, recibe el 409 común, sin cruzar posiciones ni chocar con la restricción
/// diferida (un 500).
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
