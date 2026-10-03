using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>Verifica configuración, autorización y concurrencia con PostgreSQL y una identidad aislada.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class OrganizationSettingsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_reads_updates_and_gets_conflict_for_a_stale_settings_version()
    {
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = Tenant.CreateBusiness("Configuración HTTP test", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"),
                    [account.UserId], account.UserId);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        using var initial = await client.GetAsync("/api/organization/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using var initialJson = JsonDocument.Parse(await initial.Content.ReadAsStringAsync(Ct));
        var originalVersion = initialJson.RootElement.GetProperty("version").GetUInt32();
        Assert.Equal("Configuración HTTP test", initialJson.RootElement.GetProperty("name").GetString());

        using var saved = await client.PutAsJsonAsync("/api/organization/settings", new
        {
            name = "  Configuración actualizada  ",
            defaultCulture = "en-US",
            defaultTimeZoneId = "America/Argentina/Mendoza",
            defaultCurrency = "ARS",
            version = originalVersion,
        }, Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.NoContent,
            await saved.Content.ReadAsStringAsync(Ct));

        using var updated = await client.GetAsync("/api/organization/settings", Ct);
        using var updatedJson = JsonDocument.Parse(await updated.Content.ReadAsStringAsync(Ct));
        var root = updatedJson.RootElement;
        Assert.Equal("Configuración actualizada", root.GetProperty("name").GetString());
        Assert.Equal("en-US", root.GetProperty("defaultCulture").GetString());
        Assert.Equal("America/Argentina/Mendoza", root.GetProperty("defaultTimeZoneId").GetString());
        Assert.Equal("ARS", root.GetProperty("defaultCurrency").GetString());
        Assert.NotEqual(originalVersion, root.GetProperty("version").GetUInt32());

        using var stale = await client.PutAsJsonAsync("/api/organization/settings", new
        {
            name = "Configuración vieja",
            defaultCulture = "es-AR",
            defaultTimeZoneId = "America/Argentina/Buenos_Aires",
            defaultCurrency = "ARS",
            version = originalVersion,
        }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var staleJson = JsonDocument.Parse(await stale.Content.ReadAsStringAsync(Ct));
        Assert.Equal("General.ConcurrencyConflict", staleJson.RootElement.GetProperty("code").GetString());
    }
}
