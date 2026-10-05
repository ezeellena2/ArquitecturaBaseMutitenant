using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Features;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformFeatureCivilExpiryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("UTC", 3, 8, 0)]
    [InlineData("America/Argentina/Buenos_Aires", 3, 8, 3)]
    [InlineData("America/New_York", 3, 8, 4)]
    [InlineData("America/New_York", 11, 1, 5)]
    public async Task Civil_expiry_uses_the_organization_zone_including_dst_and_readers_need_no_private_scope(
        string zone, int month, int day, int endHourUtc)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IFeatureCatalog>(new TestFeatureCatalog()))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var tenant = Tenant.CreateBusiness("Fecha civil " + Guid.NewGuid().ToString("N"), false);
        tenant.Activate();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var lease = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", zone, "ARS"), [account.UserId], account.UserId);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        var expiresOn = new DateOnly(2026, month, day);
        var expectedUtc = new DateTime(2026, month, day + 1, endHourUtc, 0, 0, DateTimeKind.Utc);
        var path = $"/api/platform/tenants/{tenant.Id:D}/features";
        using var updated = await client.PutAsJsonAsync(path + "/test-business",
            new { enabled = true, source = "Trial", expiresOn, version = 0u, reason = "Hasta el final del día" }, Ct);
        await SavedVersion.ReadAsync(updated, Ct);
        await using var check = host.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var feature = await db.TenantFeatures.AsNoTracking().SingleAsync(item => item.TenantId == tenant.Id, Ct);
        Assert.Equal(expectedUtc, feature.ExpiresAtUtc);
        Assert.Equal(expiresOn, feature.ExpiresOn);
        Assert.True(feature.IsEnabledAt(expectedUtc.AddTicks(-1)));
        Assert.False(feature.IsEnabledAt(expectedUtc));
        var enteredBeforeRead = await db.SecurityEvents.CountAsync(item => item.TargetTenantId == tenant.Id, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.Tenants.Read], Ct);
        using var read = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync(Ct));
        var item = json.RootElement.EnumerateArray().Single(row => row.GetProperty("key").GetString() == "test-business");
        Assert.Equal(expiresOn, DateOnly.Parse(item.GetProperty("expiresOn").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(item.TryGetProperty("expiresAtUtc", out _));
        Assert.Equal(enteredBeforeRead, await db.SecurityEvents.CountAsync(item => item.TargetTenantId == tenant.Id, Ct));
    }

    [Fact]
    public async Task Legacy_utc_expiry_is_preserved_without_inventing_a_civil_date()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IFeatureCatalog>(new TestFeatureCatalog()))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.Reduced, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var tenant = Tenant.CreateBusiness("Prueba histórica " + Guid.NewGuid().ToString("N"), false);
        tenant.Activate();
        var expiryUtc = new DateTime(2026, 10, 1, 17, 23, 0, DateTimeKind.Utc);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ITenantRepository>().Add(tenant);
                services.GetRequiredService<ITenantFeatureRepository>().Add(
                    TenantFeature.Create(tenant.Id, "test-business", true, TenantFeatureSource.Trial, expiryUtc).Value);
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                    AuditActorKind.System, Guid.Empty, tenant.Id, "Configuración histórica", expiryUtc));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var response = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}/features", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var item = json.RootElement.EnumerateArray().Single(row => row.GetProperty("key").GetString() == "test-business");
        Assert.Equal(JsonValueKind.Null, item.GetProperty("expiresOn").ValueKind);
        await using var check = host.Services.CreateAsyncScope();
        var feature = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().TenantFeatures.AsNoTracking()
            .SingleAsync(item => item.TenantId == tenant.Id, Ct);
        Assert.Equal(expiryUtc, feature.ExpiresAtUtc);
        Assert.False(feature.IsEnabledAt(expiryUtc));
    }
}
