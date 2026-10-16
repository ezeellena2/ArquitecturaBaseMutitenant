using System.Net;
using System.Text;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using StackExchange.Redis;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// ADR 0052 con APIs independientes sobre la misma base y el mismo Redis: lo que confirma una lo lee la otra sin esperar
/// el vencimiento; un commit fallido no avanza nada; una carga que se cruza con un commit nunca guarda datos viejos con la
/// generación nueva; Redis caído no rompe el negocio ni resucita entradas al volver; otro ambiente no comparte entradas;
/// un sobre incompatible es un miss; y los correos de contacto nunca llegan a Redis.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class DistributedCacheTests(ApiFactory factory)
{
    private const string DeadRedis = "127.0.0.1:1";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_commit_in_one_api_is_read_by_the_other_without_waiting_for_expiration()
    {
        var reads = new TenantReads();
        await using var writer = Host();
        await using var reader = Host(reads);
        var (tenantId, _) = await NewOrganizationAsync(writer.Services);
        var statuses = reader.Services.GetRequiredService<ITenantStatusCache>();

        Assert.Equal(TenantStatus.Active, await statuses.GetStatusAsync(tenantId, Ct));
        Assert.Equal(TenantStatus.Active, await statuses.GetStatusAsync(tenantId, Ct));
        Assert.Equal(1, reads.Count);

        await SuspendAsync(writer.Services, tenantId);

        Assert.Equal(TenantStatus.Suspended, await statuses.GetStatusAsync(tenantId, Ct));
        Assert.Equal(2, reads.Count);
    }

    [Fact]
    public async Task Replicas_share_the_data_protection_keys_of_their_cookies()
    {
        await using var first = Host();
        await using var second = Host();
        const string Purpose = "ArquitecturaBaseMultitenant.E9.Replicas";

        // Las claves viven en PostgreSQL: lo que protege una réplica, como sus cookies, lo abre la otra.
        var protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose)
            .Protect("sesión compartida");

        Assert.Equal("sesión compartida", second.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose).Unprotect(protectedValue));
    }

    [Fact]
    public async Task A_failed_commit_neither_advances_the_generation_nor_drops_the_entry()
    {
        var reads = new TenantReads();
        await using var host = Host(reads);
        await using var failing = Host(failure: new CommitFailureProbe());
        var (tenantId, _) = await NewOrganizationAsync(host.Services);
        var statuses = host.Services.GetRequiredService<ITenantStatusCache>();
        Assert.Equal(TenantStatus.Active, await statuses.GetStatusAsync(tenantId, Ct));
        var before = await GenerationAsync(host.Services, CacheTags.Organization(tenantId));

        // El guardado llega a correr adentro de la transacción y el commit falla: la generación vuelve con el rollback.
        await Assert.ThrowsAsync<ExpectedCommitFailure>(() => SuspendAsync(failing.Services, tenantId));

        Assert.Equal(before, await GenerationAsync(host.Services, CacheTags.Organization(tenantId)));
        Assert.Equal(TenantStatus.Active, await statuses.GetStatusAsync(tenantId, Ct));
        Assert.Equal(1, reads.Count);
    }

    [Fact]
    public async Task A_load_that_crosses_a_commit_never_caches_the_old_value_under_the_new_generation()
    {
        var reads = new TenantReads();
        await using var writer = Host();
        await using var reader = Host(reads);
        var (tenantId, _) = await NewOrganizationAsync(writer.Services);
        var statuses = reader.Services.GetRequiredService<ITenantStatusCache>();

        reads.PauseNext();
        var crossing = statuses.GetStatusAsync(tenantId, Ct);
        await reads.Paused.Task.WaitAsync(Wait, Ct);
        await SuspendAsync(writer.Services, tenantId);
        reads.Resume.TrySetResult();

        // La carga leyó «activa» antes del commit; al comprobar las generaciones la descarta y vuelve a leer.
        Assert.Equal(TenantStatus.Suspended, await crossing.WaitAsync(Wait, Ct));
        Assert.Equal(2, reads.Count);
        Assert.Equal(TenantStatus.Suspended, await statuses.GetStatusAsync(tenantId, Ct));
        Assert.Equal(2, reads.Count);
    }

    [Fact]
    public async Task A_lost_invalidation_does_not_resurrect_the_old_value()
    {
        var reads = new TenantReads();
        await using var reader = Host(reads);
        await using var writerWithoutRedis = Host(redis: DeadRedis);
        var (tenantId, _) = await NewOrganizationAsync(reader.Services);
        var statuses = reader.Services.GetRequiredService<ITenantStatusCache>();
        Assert.Equal(TenantStatus.Active, await statuses.GetStatusAsync(tenantId, Ct));
        var oldKey = await PhysicalKeyAsync(reader.Services, CacheKeys.Tenant(tenantId, "status"),
            CacheTags.Organization(tenantId));

        // La API que escribe no llega a Redis: su invalidación después del commit se pierde.
        await SuspendAsync(writerWithoutRedis.Services, tenantId);

        await using var redis = await ConnectionMultiplexer.ConnectAsync(factory.CacheConnectionString);
        Assert.True(await redis.GetDatabase().KeyExistsAsync(oldKey));
        Assert.Equal(TenantStatus.Suspended, await statuses.GetStatusAsync(tenantId, Ct));
        Assert.Equal(2, reads.Count);
    }

    [Fact]
    public async Task Without_redis_the_business_answers_and_only_readiness_fails()
    {
        await using var host = Host(redis: DeadRedis);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var (tenantId, _) = await NewOrganizationAsync(host.Services);

        Assert.Equal(TenantStatus.Active,
            await host.Services.GetRequiredService<ITenantStatusCache>().GetStatusAsync(tenantId, Ct));
        // El alta y el ingreso completos pasan por las cachés de estado y de referencias.
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        using (var me = await client.GetAsync("/api/me", Ct))
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.NotEqual(Guid.Empty, account.UserId);

        using (var alive = await client.GetAsync("/alive", Ct))
            Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        using (var ready = await client.GetAsync("/health", Ct))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task Another_environment_never_shares_entries()
    {
        var reads = new TenantReads();
        var otherReads = new TenantReads();
        await using var host = Host(reads);
        await using var other = Host(otherReads, environment: "Aislado");
        var (tenantId, _) = await NewOrganizationAsync(host.Services);

        Assert.Equal(TenantStatus.Active,
            await host.Services.GetRequiredService<ITenantStatusCache>().GetStatusAsync(tenantId, Ct));
        Assert.Equal(TenantStatus.Active,
            await other.Services.GetRequiredService<ITenantStatusCache>().GetStatusAsync(tenantId, Ct));

        Assert.Equal(1, reads.Count);
        Assert.Equal(1, otherReads.Count);
    }

    [Fact]
    public async Task An_incompatible_payload_is_a_miss_and_is_replaced()
    {
        var reads = new TenantReads();
        await using var host = Host(reads);
        var (tenantId, _) = await NewOrganizationAsync(host.Services);
        var statuses = host.Services.GetRequiredService<ITenantStatusCache>();
        Assert.Equal(TenantStatus.Active, await statuses.GetStatusAsync(tenantId, Ct));
        var key = await PhysicalKeyAsync(host.Services, CacheKeys.Tenant(tenantId, "status"), CacheTags.Organization(tenantId));
        await using var redis = await ConnectionMultiplexer.ConnectAsync(factory.CacheConnectionString);
        var database = redis.GetDatabase();
        var stored = await PayloadAsync(database, key);
        Assert.NotNull(stored);

        var incompatible = Encoding.UTF8.GetBytes("{\"SchemaVersion\":0,\"Value\":\"Suspended\"}");
        await WritePayloadAsync(database, key, incompatible);

        Assert.Equal(TenantStatus.Active, await statuses.GetStatusAsync(tenantId, Ct));
        Assert.Equal(2, reads.Count);
        Assert.NotEqual(incompatible, await PayloadAsync(database, key));
        Assert.Equal(TenantStatus.Active, await statuses.GetStatusAsync(tenantId, Ct));
        Assert.Equal(2, reads.Count);
    }

    [Fact]
    public async Task Contact_emails_never_reach_redis()
    {
        const string Sales = "ventas-cache@example.test";
        const string Support = "soporte-cache@example.test";
        await using var host = Host();
        await host.Services.GetRequiredService<Infrastructure.Persistence.Seed.DatabaseSeeder>().SeedAsync(Ct);
        var previous = await ContactsAsync(host.Services);
        try
        {
            await SetContactsAsync(host.Services, Sales, Support);
            var settings = await ReadSettingsAsync(host.Services);
            Assert.Equal(Sales, settings!.SalesEmail?.Value);
            Assert.Equal(Support, settings.SupportEmail?.Value);
            // Una lectura más sale de Redis y sigue trayendo los correos, leídos aparte de PostgreSQL.
            Assert.Equal(Sales, (await ReadSettingsAsync(host.Services))!.SalesEmail?.Value);

            await using var redis = await ConnectionMultiplexer.ConnectAsync(factory.CacheConnectionString);
            var database = redis.GetDatabase();
            var server = redis.GetServers().Single();
            var keys = server.Keys(pattern: "arquitecturabasemt:*").ToArray();
            Assert.Contains(keys, key => key.ToString().Contains(":p:settings:", StringComparison.Ordinal));
            foreach (var key in keys)
            {
                var bytes = await PayloadAsync(database, key.ToString());
                var text = bytes is null ? string.Empty : Encoding.UTF8.GetString(bytes);
                Assert.DoesNotContain("example.test", text, StringComparison.Ordinal);
            }
        }
        finally
        {
            await SetContactsAsync(host.Services, previous.Sales, previous.Support);
        }
    }

    private WebApplicationFactory<Program> Host(TenantReads? reads = null, string? redis = null,
        string? environment = null, CommitFailureProbe? failure = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (redis is not null) builder.UseSetting("ConnectionStrings:cache", redis);
            if (environment is not null) builder.UseSetting("Cache:Environment", environment);
            builder.ConfigureTestServices(services =>
            {
                if (reads is not null)
                {
                    var registration = services.Last(service => service.ServiceType == typeof(ITenantReader));
                    services.Replace(ServiceDescriptor.Scoped<ITenantReader>(provider => new CountingTenantReader(
                        (ITenantReader)ActivatorUtilities.CreateInstance(provider, registration.ImplementationType!), reads)));
                }

                if (failure is not null) FailingCommitUnitOfWork.Replace(services, failure);
            });
        });

    private static async Task<(Guid TenantId, Guid UserId)> NewOrganizationAsync(IServiceProvider root)
    {
        await using var scope = root.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var work = services.GetRequiredService<IUnitOfWork>();
        var userId = Guid.Empty;
        await work.ExecuteInTransactionAsync(async ct =>
        {
            userId = (await services.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR",
                "America/Argentina/Buenos_Aires", ct)).Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var tenant = Tenant.CreateBusiness("Caché " + Guid.NewGuid().ToString("N")[..8], requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        using (services.GetRequiredService<ITenantScope>().Enter(tenant.Id))
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        return (tenant.Id, userId);
    }

    private static async Task SuspendAsync(IServiceProvider root, Guid tenantId)
    {
        await using var scope = root.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var tracked = await services.GetRequiredService<ITenantRepository>().GetByIdAsync(tenantId, ct);
            Assert.NotNull(tracked);
            return tracked.Suspend();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
    }

    private static async Task<long?> GenerationAsync(IServiceProvider root, string tag)
    {
        await using var scope = root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().CacheTagVersions.AsNoTracking()
            .Where(version => version.Tag == tag).Select(version => (long?)version.Generation).SingleOrDefaultAsync(Ct);
    }

    // La clave física de una entrada con una sola etiqueta: prefijo, esquema, identidad de la base y generación.
    private static async Task<string> PhysicalKeyAsync(IServiceProvider root, string logicalKey, string tag)
    {
        var generations = CacheKeyFormat.Generations(
            [await GenerationAsync(root, CacheTags.Database) ?? 0, await GenerationAsync(root, tag) ?? 0]);
        return "arquitecturabasemt:Testing:" + SharedCache.Key(new CacheEntry(logicalKey, [tag], TimeSpan.Zero), generations);
    }

    // Redis guarda cada entrada de HybridCache como un hash con su campo «data».
    private static async Task<byte[]?> PayloadAsync(IDatabase database, string key) =>
        await database.KeyTypeAsync(key) == RedisType.Hash
            ? (byte[]?)await database.HashGetAsync(key, "data")
            : (byte[]?)await database.StringGetAsync(key);

    private static async Task WritePayloadAsync(IDatabase database, string key, byte[] payload)
    {
        if (await database.KeyTypeAsync(key) == RedisType.Hash) await database.HashSetAsync(key, "data", payload);
        else await database.StringSetAsync(key, payload, keepTtl: true);
    }

    private static async Task<(string? Sales, string? Support)> ContactsAsync(IServiceProvider root)
    {
        await using var scope = root.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlatformSettings.AsNoTracking()
            .Where(settings => settings.Id == PlatformSettings.SingletonId)
            .Select(settings => new { settings.SalesEmail, settings.SupportEmail }).SingleAsync(Ct);
        return (row.SalesEmail?.Value, row.SupportEmail?.Value);
    }

    // Por SQL, sin el evento de Plataforma: la prueba sólo necesita la fila y avanza la generación como lo haría el commit.
    private async Task SetContactsAsync(IServiceProvider root, string? sales, string? support)
    {
        await using (var connection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                UPDATE platform."PlatformSettings" SET "SalesEmail" = @sales, "SupportEmail" = @support
                """, connection);
            command.Parameters.AddWithValue("sales", (object?)sales ?? DBNull.Value);
            command.Parameters.AddWithValue("support", (object?)support ?? DBNull.Value);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
        }

        await CacheGenerations.AdvanceAsync(root, Ct, CacheTags.PlatformConfiguration);
    }

    private static async Task<PlatformSettingsRow?> ReadSettingsAsync(IServiceProvider root)
    {
        await using var scope = root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPlatformSettingsReader>().FindAsync(Ct);
    }

    private sealed class TenantReads
    {
        private int _count;
        private int _pause;

        internal int Count => Volatile.Read(ref _count);

        internal TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void PauseNext() => Volatile.Write(ref _pause, 1);

        internal async Task CountAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _count);
            if (Interlocked.Exchange(ref _pause, 0) == 1)
            {
                Paused.TrySetResult();
                await Resume.Task.WaitAsync(Wait, ct);
            }
        }
    }

    // Lee primero y recién después, si la prueba lo pide, espera: el valor que devuelve ya puede ser viejo.
    private sealed class CountingTenantReader(ITenantReader inner, TenantReads reads) : ITenantReader
    {
        public async Task<TenantRow?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var row = await inner.FindByIdAsync(tenantId, cancellationToken);
            await reads.CountAsync(cancellationToken);
            return row;
        }

        public Task<IReadOnlyList<TenantRow>> ListActiveBusinessesAsync(CancellationToken cancellationToken) =>
            inner.ListActiveBusinessesAsync(cancellationToken);
    }
}
