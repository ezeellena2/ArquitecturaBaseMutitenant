using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Prueba la receta B2C por HTTP con la base RLS, UoW, idempotencia e interceptores reales.</summary>
public sealed class PersonalIsolationTests(PersonalIsolationFactory factory) : IClassFixture<PersonalIsolationFactory>
{
    private const string PersonalPath = "/test/personal-widgets";
    private const string BusinessPath = "/test/business-widgets";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task People_never_reach_each_other_personal_data()
    {
        using var kevin = await PersonalIsolationJourney.CreateAsync(factory);
        using var carla = await PersonalIsolationJourney.CreateAsync(factory);
        Assert.NotEqual(kevin.PersonalId, carla.PersonalId);
        var kevins = await CreateAsync(kevin.Client, "Privado de Kevin");
        var carlas = await CreateAsync(carla.Client, "Privado de Carla");

        await AssertForeignCrudAsync(kevin.Client, carlas);
        await AssertForeignCrudAsync(carla.Client, kevins);
        Assert.Equal(kevins.Id, Assert.Single((await PageAsync(kevin.Client)).Items).Id);
        Assert.Equal(carlas.Id, Assert.Single((await PageAsync(carla.Client)).Items).Id);
        Assert.Equal(kevins, await GetAsync(kevin.Client, kevins.Id));
        Assert.Equal(carlas, await GetAsync(carla.Client, carlas.Id));
    }

    [Fact]
    public async Task The_consumer_and_business_access_of_one_person_do_not_mix()
    {
        using var kevin = await PersonalIsolationJourney.CreateAsync(factory);
        var personal = await CreateAsync(kevin.Client, "Kevin como persona");
        var businessId = await kevin.CreateBusinessAsync();
        Assert.NotEqual(kevin.PersonalId, businessId);
        var business = await kevin.AddBusinessWidgetAsync(businessId, "Privado de la empresa");

        using (var businessRoute = await kevin.Client.GetAsync($"{BusinessPath}/{business.Id}", Ct))
            await AssertProblemAsync(businessRoute, HttpStatusCode.Forbidden, AccessErrors.WrongCode);
        using (var ownRouteForeignId = await kevin.Client.GetAsync($"{PersonalPath}/{business.Id}", Ct))
            await AssertProblemAsync(ownRouteForeignId, HttpStatusCode.NotFound, "Http.NotFound");
        await AssertForeignCrudAsync(kevin.Client, new PersonalWidgetResponse(business.Id, business.Name, business.Version));
        Assert.Equal(personal.Id, Assert.Single((await PageAsync(kevin.Client)).Items).Id);

        await kevin.BusinessAsync(businessId);
        var inbox = await PageAsync(kevin.Client, BusinessPath);
        Assert.Equal(business.Id, Assert.Single(inbox.Items).Id);
        using (var privatePersonalId = await kevin.Client.GetAsync($"{BusinessPath}/{personal.Id}", Ct))
            await AssertProblemAsync(privatePersonalId, HttpStatusCode.NotFound, "Http.NotFound");
        using (var wrongList = await kevin.Client.GetAsync(PersonalPath, Ct))
            await AssertProblemAsync(wrongList, HttpStatusCode.Forbidden, AccessErrors.WrongCode);
        using (var wrongGet = await kevin.Client.GetAsync($"{PersonalPath}/{personal.Id}", Ct))
            await AssertProblemAsync(wrongGet, HttpStatusCode.Forbidden, AccessErrors.WrongCode);
        using (var wrongCreate = await AccountJourney.PostAsync(kevin.Client, PersonalPath, new { name = "No permitido" }, Ct))
            await AssertProblemAsync(wrongCreate, HttpStatusCode.Forbidden, AccessErrors.WrongCode);
        using (var wrongUpdate = await kevin.Client.PutAsJsonAsync($"{PersonalPath}/{personal.Id}",
            new { name = "No permitido", version = personal.Version }, Ct))
            await AssertProblemAsync(wrongUpdate, HttpStatusCode.Forbidden, AccessErrors.WrongCode);
        using (var wrongDelete = await DeleteAsync(kevin.Client, personal.Id, personal.Version))
            await AssertProblemAsync(wrongDelete, HttpStatusCode.Forbidden, AccessErrors.WrongCode);

        await kevin.ConsumerAsync();
        Assert.Equal(personal, await GetAsync(kevin.Client, personal.Id));
        Assert.Equal(personal.Id, Assert.Single((await PageAsync(kevin.Client)).Items).Id);
    }

    [Fact]
    public async Task Consumer_routes_need_no_permission()
    {
        using var person = await PersonalIsolationJourney.CreateAsync(factory);
        using var me = await person.Client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Empty(me!.RootElement.GetProperty("effectivePermissions").GetProperty("organization").EnumerateArray());
        Assert.Empty(me.RootElement.GetProperty("effectivePermissions").GetProperty("companies").EnumerateObject());
        Assert.Empty(me.RootElement.GetProperty("effectivePermissions").GetProperty("platform").EnumerateArray());

        var created = await CreateAsync(person.Client, "Sin rol ni permiso");
        Assert.Equal(created.Id, Assert.Single((await PageAsync(person.Client)).Items).Id);
        using var updated = await person.Client.PutAsJsonAsync($"{PersonalPath}/{created.Id}",
            new { name = "Puede editar", version = created.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var version = await SavedVersionAsync(updated);
        var saved = await GetAsync(person.Client, created.Id);
        Assert.Equal("Puede editar", saved.Name);
        Assert.Equal(version, saved.Version);
        using var deleted = await DeleteAsync(person.Client, created.Id, version);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var hidden = await person.Client.GetAsync($"{PersonalPath}/{created.Id}", Ct);
        await AssertProblemAsync(hidden, HttpStatusCode.NotFound, "Http.NotFound");
        Assert.Empty((await PageAsync(person.Client)).Items);
    }

    [Fact]
    public async Task Personal_writes_replay_and_reject_a_stale_version_without_changing_the_row()
    {
        using var person = await PersonalIsolationJourney.CreateAsync(factory);
        var key = Guid.NewGuid();
        using var first = await PostWithKeyAsync(person.Client, "Una sola fila", key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var id = await first.Content.ReadFromJsonAsync<Guid>(Ct);
        using var replay = await PostWithKeyAsync(person.Client, "Una sola fila", key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(id, await replay.Content.ReadFromJsonAsync<Guid>(Ct));
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(1, (await PageAsync(person.Client)).TotalCount);
        var original = await GetAsync(person.Client, id);

        using var updated = await person.Client.PutAsJsonAsync($"{PersonalPath}/{id}",
            new { name = "Primer guardado", version = original.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var version = await SavedVersionAsync(updated);
        Assert.NotEqual(original.Version, version);
        using var staleUpdate = await person.Client.PutAsJsonAsync($"{PersonalPath}/{id}",
            new { name = "No debe pisar", version = original.Version }, Ct);
        await AssertProblemAsync(staleUpdate, HttpStatusCode.Conflict, "General.ConcurrencyConflict");
        using var staleDelete = await DeleteAsync(person.Client, id, original.Version);
        await AssertProblemAsync(staleDelete, HttpStatusCode.Conflict, "General.ConcurrencyConflict");
        Assert.Equal(new PersonalWidgetResponse(id, "Primer guardado", version), await GetAsync(person.Client, id));

        using var unchanged = await person.Client.PutAsJsonAsync($"{PersonalPath}/{id}",
            new { name = "Primer guardado", version }, Ct);
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        var nextVersion = await SavedVersionAsync(unchanged);
        Assert.NotEqual(version, nextVersion);
        Assert.Equal(nextVersion, (await GetAsync(person.Client, id)).Version);
        using var deleted = await DeleteAsync(person.Client, id, nextVersion);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, (await PageAsync(person.Client)).TotalCount);
    }

    [Fact]
    public async Task Personal_lists_page_and_search_only_the_active_person()
    {
        using var kevin = await PersonalIsolationJourney.CreateAsync(factory);
        using var carla = await PersonalIsolationJourney.CreateAsync(factory);
        await CreateAsync(kevin.Client, "Árbol personal");
        await CreateAsync(kevin.Client, "Literal %_");
        for (var index = 0; index < 10; index++) await CreateAsync(kevin.Client, $"Dato {index:00}");
        await CreateAsync(carla.Client, "Árbol de otra persona");

        var first = await PageAsync(kevin.Client);
        Assert.Equal(12, first.TotalCount);
        Assert.Equal(PagedRequest.DefaultPageSize, first.Items.Count);
        var second = await PageAsync(kevin.Client, PersonalPath + "?page=2&pageSize=10");
        Assert.Equal(12, second.TotalCount);
        Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(row => row.Id).Intersect(second.Items.Select(row => row.Id)));
        var outside = await PageAsync(kevin.Client, PersonalPath + "?page=3&pageSize=10");
        Assert.Empty(outside.Items);
        Assert.Equal(12, outside.TotalCount);
        Assert.Equal("Árbol personal", Assert.Single((await PageAsync(kevin.Client, PersonalPath + "?search=arbol")).Items).Name);
        Assert.Equal("Literal %_", Assert.Single((await PageAsync(kevin.Client, PersonalPath + "?search=%25%5F")).Items).Name);
        foreach (var query in new[] { "?page=0", "?pageSize=11", "?sort=unknown" })
        {
            using var invalid = await kevin.Client.GetAsync(PersonalPath + query, Ct);
            await AssertProblemAsync(invalid, HttpStatusCode.BadRequest, "Validation.Failed");
        }
    }

    [Fact]
    public async Task Personal_routes_require_an_authenticated_session()
    {
        using var client = factory.Client();
        using var list = await client.GetAsync(PersonalPath, Ct);
        await AssertProblemAsync(list, HttpStatusCode.Unauthorized, "Http.Unauthorized");
        using var create = await AccountJourney.PostAsync(client, PersonalPath, new { name = "Sin sesión" }, Ct);
        await AssertProblemAsync(create, HttpStatusCode.Unauthorized, "Http.Unauthorized");
    }

    private static async Task AssertForeignCrudAsync(HttpClient client, PersonalWidgetResponse foreign)
    {
        using var get = await client.GetAsync($"{PersonalPath}/{foreign.Id}", Ct);
        await AssertProblemAsync(get, HttpStatusCode.NotFound, "Http.NotFound");
        using var update = await client.PutAsJsonAsync($"{PersonalPath}/{foreign.Id}",
            new { name = "No debe cambiar", version = foreign.Version }, Ct);
        await AssertProblemAsync(update, HttpStatusCode.NotFound, "Http.NotFound");
        using var delete = await DeleteAsync(client, foreign.Id, foreign.Version);
        await AssertProblemAsync(delete, HttpStatusCode.NotFound, "Http.NotFound");
        Assert.DoesNotContain((await PageAsync(client)).Items, row => row.Id == foreign.Id);
    }

    private static async Task<PersonalWidgetResponse> CreateAsync(HttpClient client, string name)
    {
        using var response = await AccountJourney.PostAsync(client, PersonalPath, new { name }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var id = await response.Content.ReadFromJsonAsync<Guid>(Ct);
        return await GetAsync(client, id);
    }

    private static async Task<PersonalWidgetResponse> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"{PersonalPath}/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PersonalWidgetResponse>(Ct))!;
    }

    private static async Task<PagedResult<PersonalWidgetResponse>> PageAsync(HttpClient client, string path = PersonalPath)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<PersonalWidgetResponse>>(Ct))!;
    }

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid id, uint version) =>
        SendAsync(client, new HttpRequestMessage(HttpMethod.Delete, $"{PersonalPath}/{id}")
        {
            Content = JsonContent.Create(new { version }),
        });

    private static Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string name, Guid key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, PersonalPath) { Content = JsonContent.Create(new { name }) };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return SendAsync(client, request);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request)
    {
        using (request) return await client.SendAsync(request, Ct);
    }

    private static async Task<uint> SavedVersionAsync(HttpResponseMessage response)
    {
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return body!.RootElement.GetProperty("version").GetUInt32();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(code, body!.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }
}
