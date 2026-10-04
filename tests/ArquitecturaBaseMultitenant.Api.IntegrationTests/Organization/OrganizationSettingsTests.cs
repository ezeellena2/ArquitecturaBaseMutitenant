using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task A_save_leaves_a_single_audit_entry_whose_diff_has_only_the_edited_field()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, "Configuración auditada", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        using var initial = await client.GetAsync("/api/organization/settings", Ct);
        using var initialJson = JsonDocument.Parse(await initial.Content.ReadAsStringAsync(Ct));

        // Tablero Auditoria-Org: «Cambió el idioma y la región predeterminados», con una sola fila.
        using var saved = await client.PutAsJsonAsync("/api/organization/settings", new
        {
            name = "Configuración auditada",
            defaultCulture = "en-US",
            defaultTimeZoneId = "America/Argentina/Buenos_Aires",
            defaultCurrency = "ARS",
            version = initialJson.RootElement.GetProperty("version").GetUInt32(),
        }, Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.NoContent, await saved.Content.ReadAsStringAsync(Ct));

        await using var scope = factory.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var entries = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AuditEntry>()
            .AsNoTracking().Where(entry => entry.TenantId == tenantId && entry.EntityType == "TenantSettings"
                && entry.Action != AuditAction.Created).ToArrayAsync(Ct);
        var entry = Assert.Single(entries);
        using var diff = JsonDocument.Parse(entry.Changes);
        var change = Assert.Single(diff.RootElement.EnumerateObject());
        Assert.Equal("DefaultCulture", change.Name);
        Assert.Equal("es-AR", change.Value.GetProperty("old").GetString());
        Assert.Equal("en-US", change.Value.GetProperty("new").GetString());
    }
}
