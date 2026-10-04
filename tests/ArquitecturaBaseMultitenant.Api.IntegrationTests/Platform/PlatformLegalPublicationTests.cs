using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Plataforma › Documentos legales: cada publicación es una versión nueva e inmutable, numerada por tipo, con el texto
/// en todas las culturas habilitadas. Desde su vigencia, la versión obliga a aceptarla; una futura todavía no.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformLegalPublicationTests
{
    private const string Zone = "America/Argentina/Buenos_Aires";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_future_version_waits_and_a_current_one_requires_acceptance()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var person = Client(isolated);
        await AccountJourney.RegisterAsync(isolated, person, Ct);
        using var platform = await OperatorAsync(isolated, [PlatformPermissions.Legal.Manage]);
        var today = Today(isolated);

        var initial = await ListAsync(platform);
        Assert.Equal(1, Group(initial, "Terms").GetProperty("currentVersion").GetInt32());

        var key = Guid.NewGuid().ToString("D");
        var future = Body("Terms", today.AddDays(2));
        using var published = await SendAsync(platform, future, key);
        Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        var created = await JsonAsync(published);
        Assert.Equal(2, created.GetProperty("version").GetInt32());
        using var replayed = await SendAsync(platform, future, key);
        Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(created.GetProperty("id").GetGuid(), (await JsonAsync(replayed)).GetProperty("id").GetGuid());

        var terms = Group(await ListAsync(platform), "Terms");
        Assert.Equal(1, terms.GetProperty("currentVersion").GetInt32());
        Assert.Equal(2, terms.GetProperty("versions").GetArrayLength());
        var detail = await GetAsync(platform, "/" + created.GetProperty("id").GetGuid().ToString("D"));
        Assert.Equal(2, detail.GetProperty("version").GetInt32());
        Assert.False(detail.GetProperty("isCurrent").GetBoolean());
        Assert.Equal(["en-US", "es-AR"], detail.GetProperty("contents").EnumerateArray()
            .Select(item => item.GetProperty("culture").GetString()).Order(StringComparer.Ordinal).ToArray());
        using (var stillFine = await person.GetAsync("/api/me/login-methods", Ct))
            Assert.Equal(HttpStatusCode.OK, stillFine.StatusCode);

        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            var ev = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SecurityEvents.AsNoTracking()
                .SingleAsync(item => item.Type == SecurityEventType.LegalDocumentPublished, Ct);
            Assert.Equal(AuditActorKind.PlatformOperator, ev.ActorKind);
            Assert.Equal("Publicación de una versión de un documento legal.", ev.Reason);
            using var diff = JsonDocument.Parse(ev.Changes);
            Assert.Equal(2, diff.RootElement.GetProperty("version").GetProperty("after").GetInt32());
            Assert.DoesNotContain("Términos", ev.Changes, StringComparison.Ordinal);
        }

        using var current = await SendAsync(platform, Body("Privacy", today), Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Created, current.StatusCode);
        using var blocked = await person.GetAsync("/api/me/login-methods", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(LegalErrors.AcceptanceRequiredCode, (await JsonAsync(blocked)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Publishing_requires_every_culture_a_valid_date_legal_manage_and_numbers_versions_once()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var platform = await OperatorAsync(isolated, [PlatformPermissions.Legal.Manage]);
        var future = Today(isolated).AddDays(5);

        foreach (var invalid in new object[]
                 {
                     new { kind = "Terms", effectiveOn = future.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), contents = new[] { new { culture = "es-AR", text = "Solo español." } } },
                     new { kind = "Terms", effectiveOn = future.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), contents = new[] { new { culture = "es-AR", text = "Texto." }, new { culture = "en-US", text = " " } } },
                     new { kind = "Terms", effectiveOn = future.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), contents = new[] { new { culture = "es-AR", text = "Texto." }, new { culture = "en-US", text = "Text." }, new { culture = "fr-FR", text = "Texte." } } },
                     Body("Terms", Today(isolated).AddDays(-1)),
                     new { kind = "Otro", effectiveOn = future.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), contents = Array.Empty<object>() },
                 })
        {
            using var rejected = await SendAsync(platform, invalid, Guid.NewGuid().ToString("D"));
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        var responses = await Task.WhenAll(
            SendAsync(platform, Body("Terms", future), Guid.NewGuid().ToString("D")),
            SendAsync(platform, Body("Terms", future.AddDays(1)), Guid.NewGuid().ToString("D")));
        var versions = new List<int>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                versions.Add((await JsonAsync(response)).GetProperty("version").GetInt32());
            }
        }
        Assert.Equal([2, 3], versions.Order().ToArray());

        using var reader = await OperatorAsync(isolated, [PlatformPermissions.Audit.Read]);
        using var forbidden = await SendAsync(reader, Body("Privacy", future), Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var anonymous = Client(isolated);
        using var unauthenticated = await anonymous.GetAsync("/api/platform/legal-documents", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        await using var scope = isolated.Services.CreateAsyncScope();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LegalDocuments
            .CountAsync(document => document.Kind == LegalDocumentKind.Terms, Ct));
    }

    private static HttpClient Client(ApiFactory api) =>
        api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<HttpClient> OperatorAsync(ApiFactory api, IReadOnlyCollection<string> permissions)
    {
        var client = Client(api);
        var account = await AccountJourney.RegisterAsync(api, client, Ct);
        await PlatformOperators.GrantAsync(api.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }

    private static DateOnly Today(ApiFactory api)
    {
        var now = api.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(Zone)).DateTime);
    }

    private static object Body(string kind, DateOnly effectiveOn) => new
    {
        kind,
        effectiveOn = effectiveOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        contents = new[]
        {
            new { culture = "es-AR", text = "Términos de la versión de prueba." },
            new { culture = "en-US", text = "Terms of the test version." },
        },
    };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, object body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/platform/legal-documents") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> ListAsync(HttpClient client) => await GetAsync(client, string.Empty);

    private static JsonElement Group(JsonElement list, string kind) =>
        list.EnumerateArray().Single(item => item.GetProperty("kind").GetString() == kind);

    private static async Task<JsonElement> GetAsync(HttpClient client, string suffix)
    {
        using var response = await client.GetAsync("/api/platform/legal-documents" + suffix, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await JsonAsync(response);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
}
