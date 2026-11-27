using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// Apagar no borra datos (ADR 0069): Plataforma apaga Formularios de una organización con su <c>PUT</c>, sus rutas dan 404 y sus
/// catálogos y sus opciones siguen en la base; al prenderlo otra vez se ven igual.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormsModuleOffKeepsDataTests(ApiFactory factory)
{
    private const string Catalogs = FormsTestData.Catalogs;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Turning_it_off_hides_its_routes_keeps_its_catalogs_and_options_and_turning_it_on_shows_them_the_same()
    {
        using var platform = FormsTestData.Client(factory);
        var operatorAccount = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var client = FormsTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Formularios apagados " + Guid.NewGuid().ToString("N")[..8], Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);

        // Plataforma lo prende y la organización arma un catálogo a mano con dos opciones.
        var version = await TurnAsync(platform, tenant, true, 0);
        var catalogId = await CreateAsync(client);
        var before = await ViewAsync(client, catalogId);
        var rows = await CountAsync(tenant);
        Assert.Equal((1, 2), rows);

        // Apagado: sus rutas dan 404 y sus filas siguen en la base.
        version = await TurnAsync(platform, tenant, false, version);
        foreach (var path in new[] { Catalogs, Catalogs + "/filter-counts", $"{Catalogs}/{catalogId:D}", $"{Catalogs}/{catalogId:D}/items",
                     $"{Catalogs}/{catalogId:D}/export", Catalogs + "/template" })
        {
            using var hidden = await client.GetAsync(path, Ct);
            Assert.True(hidden.StatusCode == HttpStatusCode.NotFound, $"{path}: {(int)hidden.StatusCode}");
        }
        Assert.Equal(rows, await CountAsync(tenant));

        // Prendido otra vez, se ve igual.
        await TurnAsync(platform, tenant, true, version);
        var after = await ViewAsync(client, catalogId);
        Assert.Equal(before, after);
        Assert.Equal(rows, await CountAsync(tenant));
    }

    /// <summary>Plataforma prende o apaga el módulo con la pestaña «Módulos», con la versión que leyó.</summary>
    private static async Task<uint> TurnAsync(HttpClient platform, Guid tenant, bool enabled, uint version)
    {
        using var saved = await platform.PutAsJsonAsync($"/api/platform/tenants/{tenant:D}/features/forms",
            new { enabled, source = "Manual", version, reason = enabled ? "Prender Formularios" : "Apagar Formularios" }, Ct);
        return await SavedVersion.ReadAsync(saved, Ct);
    }

    private static async Task<Guid> CreateAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Catalogs)
        {
            Content = JsonContent.Create(new
            {
                name = "Talles", source = "Manual",
                items = new[]
                {
                    new { localId = "n1", code = "S", text = "Chico", isActive = true },
                    new { localId = "n2", code = "M", text = "Mediano", isActive = true },
                },
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var created = await client.SendAsync(request, Ct);
        var body = await created.Content.ReadAsStringAsync(Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{(int)created.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("catalog").GetProperty("id").GetGuid();
    }

    /// <summary>El catálogo como lo ve la organización: su fila del listado, su detalle y sus opciones, en texto.</summary>
    private static async Task<(string Row, string Detail, string Items)> ViewAsync(HttpClient client, Guid catalogId)
    {
        using var list = JsonDocument.Parse(await ReadAsync(client, Catalogs));
        var row = list.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == catalogId);
        return (row.GetRawText(), await ReadAsync(client, $"{Catalogs}/{catalogId:D}"),
            await ReadAsync(client, $"{Catalogs}/{catalogId:D}/items?status=All"));
    }

    private static async Task<string> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");
        return body;
    }

    private async Task<(int Catalogs, int Items)> CountAsync(Guid tenant)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Set<FormCatalog>().CountAsync(Ct), await db.Set<FormCatalogItem>().CountAsync(Ct));
    }
}
