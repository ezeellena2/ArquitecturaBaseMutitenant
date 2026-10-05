using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Features;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformFeatureAdministrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Platform_edits_business_modules_with_reason_version_and_same_commit_event()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var host = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IFeatureCatalog>(new TestFeatureCatalog()))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var anonymous = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}/features", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        using var personalMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var personalId = personalMe!.RootElement.GetProperty("activeTenantId").GetGuid();
        await SampleAccountsFixture.SeedAsync(host.Services, account.Email.Value, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        using var businessMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var tenantId = businessMe!.RootElement.GetProperty("activeTenantId").GetGuid();
        var path = $"/api/platform/tenants/{tenantId:D}/features";
        using var businessAccess = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, businessAccess.StatusCode);
        await GrantAsync(host.Services, account.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var initial = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using var rows = await initial.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(2, rows!.RootElement.GetArrayLength());
        Assert.Equal(0u, rows.RootElement.EnumerateArray().Single(row => row.GetProperty("key").GetString() == "test-business").GetProperty("version").GetUInt32());
        var nowUtc = host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var expires = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, nowUtc.Minute, nowUtc.Second,
            DateTimeKind.Utc).AddMinutes(10);
        using var invalid = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = true, source = "Trial", version = 0u, reason = "Falta fecha" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var saved = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = true, source = "Trial", expiresAtUtc = expires, version = 0u, reason = "Prueba contratada" }, Ct);
        var savedVersion = await SavedVersion.ReadAsync(saved, Ct);
        using var current = await client.GetFromJsonAsync<JsonDocument>(path, Ct);
        var currentRow = current!.RootElement.EnumerateArray().Single(row => row.GetProperty("key").GetString() == "test-business");
        var version = currentRow.GetProperty("version").GetUInt32();
        Assert.NotEqual(0u, version);
        Assert.Equal(savedVersion, version);
        Assert.Equal(expires, currentRow.GetProperty("expiresAtUtc").GetDateTime());
        using var stale = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = false, source = "Manual", version = 0u, reason = "Versión vieja" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var noReason = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = false, source = "Manual", version, reason = " " }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        using var unknown = await client.PutAsJsonAsync(path + "/test-unknown", new
        { enabled = true, source = "Manual", version = 0u, reason = "Módulo inexistente" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var wrongScope = await client.PutAsJsonAsync(path + "/test-consumer", new
        { enabled = true, source = "Manual", version = 0u, reason = "Acceso incorrecto" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrongScope.StatusCode);
        using var personal = await client.GetAsync($"/api/platform/tenants/{personalId:D}/features", Ct);
        Assert.Equal(HttpStatusCode.NotFound, personal.StatusCode);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PutAsJsonAsync(path + "/test-emergency", new
        { enabled = false, source = "Manual", version = 0u, reason = "Cambio concurrente del valor inicial" }, Ct)));
        try
        {
            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], concurrent.Select(response => response.StatusCode).Order());
        }
        finally { foreach (var response in concurrent) response.Dispose(); }
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await context.SecurityEvents.CountAsync(row => row.Type == SecurityEventType.TenantFeaturesChanged && row.TargetTenantId == tenantId, Ct));
            var feature = await context.TenantFeatures.AsNoTracking().SingleAsync(row => row.TenantId == tenantId && row.FeatureKey == "test-business", Ct);
            Assert.True(feature.Enabled);
            Assert.Equal(expires, feature.ExpiresAtUtc);
            var events = await context.SecurityEvents.AsNoTracking().Where(row => row.Type == SecurityEventType.TenantFeaturesChanged
                && row.TargetTenantId == tenantId).ToListAsync(Ct);
            var trial = Assert.Single(events, row => row.Reason == "Prueba contratada");
            using var diff = JsonDocument.Parse(trial.Changes);
            Assert.Equal("Trial", diff.RootElement.GetProperty("features.test-business.source").GetProperty("after").GetString());
            Assert.Equal(expires, diff.RootElement.GetProperty("features.test-business.expiresAtUtc").GetProperty("after").GetDateTime());
            Assert.DoesNotContain("Prueba contratada", trial.Changes);
            Assert.DoesNotContain(account.Email.Value, trial.Changes);
            Assert.Null(trial.TargetAccountId);
        }
        // El siguiente guardado sale con la versión que respondió el anterior, sin releer.
        using var extended = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = true, source = "Trial", expiresAtUtc = expires, version = savedVersion, reason = "Prueba extendida" }, Ct);
        Assert.NotEqual(savedVersion, await SavedVersion.ReadAsync(extended, Ct));
        await GrantAsync(host.Services, account.UserId, PlatformOperators.Reduced);
        using var support = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = false, source = "Manual", version, reason = "Sin permiso" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, support.StatusCode);
        using var supportRead = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, supportRead.StatusCode);
    }

    private static Task GrantAsync(IServiceProvider provider, Guid userId, IReadOnlyCollection<string> permissions)
        => PlatformOperators.GrantAsync(provider, userId, permissions, Ct);
}
