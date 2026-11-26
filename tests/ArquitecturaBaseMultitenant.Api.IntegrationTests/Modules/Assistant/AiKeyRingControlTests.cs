using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// 0064 §11 (B15): dos anillos contra la misma base. La primera copia escribe el control de cada clave de Data Protection y una
/// copia con el mismo anillo coincide; una con otro nombre de aplicación (las mismas claves) o con otras claves no coincide, no
/// escribe nada, queda fuera de la readiness («ai-keyring») y su worker no toma turnos.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AiKeyRingControlTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_rings_against_the_same_database_only_the_first_ring_is_ready_and_the_other_writes_nothing()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("consumer", workerEnabled: false);
        Assert.Empty(await CheckAsync(session.Host));
        var written = await ControlsAsync(session.Host);
        Assert.NotEmpty(written);
        Assert.Equal(HealthStatus.Healthy, await HealthAsync(session.Host));

        await using var renamed = session.Host.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDataProtection().SetApplicationName("OtraInstalacion")));
        Assert.Equal(written.Keys.Order(), await CheckAsync(renamed));
        Assert.Equal(HealthStatus.Unhealthy, await HealthAsync(renamed));
        Assert.False(await renamed.Services.GetRequiredService<AiKeyRingReadiness>().MatchesAsync(Ct));

        var folder = Directory.CreateTempSubdirectory("anillo-");
        try
        {
            await using var otherKeys = session.Host.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddDataProtection().PersistKeysToFileSystem(folder)));
            var mismatched = await CheckAsync(otherKeys);
            Assert.NotEmpty(mismatched);
            Assert.DoesNotContain(mismatched, written.ContainsKey);
            Assert.Equal(HealthStatus.Unhealthy, await HealthAsync(otherKeys));
        }
        finally { folder.Delete(recursive: true); }

        var after = await ControlsAsync(session.Host);
        Assert.Equal(written.Keys.Order(), after.Keys.Order());
        Assert.All(written, control => Assert.Equal(control.Value, after[control.Key]));
        await using var replica = session.Replica();
        Assert.Empty(await CheckAsync(replica));
        Assert.Equal(HealthStatus.Healthy, await HealthAsync(replica));
    }

    /// <summary>
    /// Dos copias que arrancan a la vez contra una base sin controles: la primera escribe y, antes de confirmar, la segunda llega.
    /// El lock la hace esperar y leer lo confirmado: las dos quedan listas y cada control se escribe una vez. Sin el lock, la
    /// segunda leería vacío y chocaría con la clave de la primera.
    /// </summary>
    [Fact]
    public async Task Two_copies_that_start_at_once_against_a_database_without_controls_are_both_ready()
    {
        var hold = new CommitHold(context => context.ChangeTracker.Entries<AiKeyRingControl>().Any());
        await using var session = await AssistantTurnTestSession.CreateAsync("consumer", workerEnabled: false, configure: hold.Install);
        Assert.Empty(await ControlsAsync(session.Host));

        var (first, second) = await hold.RaceAsync(session.Host.Services, () => CheckAsync(session.Host), () => CheckAsync(session.Host), Ct);

        Assert.Empty(await first);
        Assert.Empty(await second);
        var written = await ControlsAsync(session.Host);
        Assert.NotEmpty(written);
        Assert.Empty(await CheckAsync(session.Host));
        Assert.Equal(written.Keys.Order(), (await ControlsAsync(session.Host)).Keys.Order());
    }

    [Fact]
    public async Task The_application_cannot_delete_or_change_a_control()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("consumer", workerEnabled: false);
        Assert.Empty(await CheckAsync(session.Host));
        await using var scope = session.Host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlAsync($"DELETE FROM platform.\"AiKeyRingControls\"", Ct));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlAsync($"UPDATE platform.\"AiKeyRingControls\" SET \"Control\" = {new byte[] { 0 }}", Ct));
    }

    private static async Task<IReadOnlyList<Guid>> CheckAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAiKeyRingControlService>().CheckAsync(Ct);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Dictionary<Guid, byte[]>> ControlsAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AiKeyRingControl>().AsNoTracking()
            .ToDictionaryAsync(row => row.KeyId, row => row.Control, Ct);
    }

    private static async Task<HealthStatus> HealthAsync(WebApplicationFactory<Program> host)
    {
        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(
            registration => registration.Name == AiKeyRingHealthCheck.Name, Ct);
        return Assert.Single(report.Entries).Value.Status;
    }
}
