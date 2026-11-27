using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Configuration;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// La barrera de las operaciones de archivo de los catálogos (C11) con la base real: una por persona, que se toma sin esperar y se
/// suelta al terminar; una vencida se retoma; otra persona no espera; la tercera de una organización da <c>TooManyAtOnce</c> y otra
/// organización no espera; el tope de la instalación cuenta todas las organizaciones; una barrera en uso se renueva y no vence
/// mientras dura su operación. Cada prueba corre en su propio momento, lejos del reloj de las demás, así las barreras de otras
/// pruebas no cuentan.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormWorkLeaseTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_same_person_twice_is_busy_another_person_does_not_wait_and_a_released_barrier_is_taken_again()
    {
        var clock = Clock();
        var store = Store(await OrganizationAsync(), clock);
        var person = Guid.NewGuid();

        var preview = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Preview, Ct));
        Assert.Equal(FormCatalogImportErrors.BusyCode, Refused(await store.TryTakeAsync(person, FormWorkLeaseKind.Export, Ct)).Code);
        await using (Taken(await store.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Apply, Ct)))
        {
        }

        await preview.DisposeAsync();
        await preview.DisposeAsync();
        await using var export = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Export, Ct));
    }

    [Fact]
    public async Task An_expired_barrier_is_taken_again_and_its_late_release_keeps_the_new_one()
    {
        var clock = Clock();
        var tenantId = await OrganizationAsync();
        var store = Store(tenantId, clock);
        var person = Guid.NewGuid();

        // La copia que la tenía se cayó sin soltarla ni renovarla: a los dos minutos vence sola y la persona vuelve a tomarla.
        await LeaveAsync(tenantId, person, clock.GetUtcNow().UtcDateTime + FormWorkLease.Lifetime);
        clock.Advance(FormWorkLease.Lifetime - TimeSpan.FromSeconds(1));
        Assert.Equal(FormCatalogImportErrors.BusyCode, Refused(await store.TryTakeAsync(person, FormWorkLeaseKind.Preview, Ct)).Code);
        clock.Advance(TimeSpan.FromSeconds(1));
        var stale = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Apply, Ct));

        // Si no se pudo renovar y venció, la persona toma otra; cuando la de antes termina tarde, suelta la suya, no la nueva.
        await ExpireAsync(tenantId, person, clock.GetUtcNow().UtcDateTime);
        await using var current = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Preview, Ct));
        await stale.DisposeAsync();
        Assert.Equal(FormCatalogImportErrors.BusyCode, Refused(await store.TryTakeAsync(person, FormWorkLeaseKind.Export, Ct)).Code);
    }

    [Fact]
    public async Task A_barrier_in_use_is_renewed_and_does_not_expire_while_its_operation_runs()
    {
        var clock = Clock();
        var tenantId = await OrganizationAsync();
        var store = Store(tenantId, clock);
        var person = Guid.NewGuid();
        var slow = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Preview, Ct));

        // Una subida lenta que dura más que el vencimiento: cada renovación corre el vencimiento.
        for (var elapsed = TimeSpan.Zero; elapsed <= FormWorkLease.Lifetime; elapsed += FormWorkLeaseStore.RenewEvery)
        {
            clock.Advance(FormWorkLeaseStore.RenewEvery);
            var expected = clock.GetUtcNow().UtcDateTime + FormWorkLease.Lifetime;
            await WaitUntilAsync(async () => await ExpiresAtAsync(tenantId, person) >= expected.AddMilliseconds(-1));
        }
        Assert.Equal(FormCatalogImportErrors.BusyCode, Refused(await store.TryTakeAsync(person, FormWorkLeaseKind.Export, Ct)).Code);

        // Al terminar se suelta y deja de renovarse.
        await slow.DisposeAsync();
        await using var next = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Export, Ct));
    }

    [Fact]
    public async Task A_release_the_database_does_not_answer_does_not_fail_the_operation_and_the_barrier_expires_alone()
    {
        var clock = Clock();
        var tenantId = await OrganizationAsync();
        // Un segundo de espera por comando: la fila tomada por otra transacción hace vencer el DELETE de la barrera.
        var impatient = new NpgsqlConnectionStringBuilder(factory.RuntimeConnectionString) { CommandTimeout = 1 }.ConnectionString;
        var store = Store(tenantId, clock, connectionString: impatient);
        var person = Guid.NewGuid();
        var lease = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Apply, Ct));

        await using (var holder = await OpenAdminAsync(tenantId))
        await using (var transaction = await holder.BeginTransactionAsync(Ct))
        {
            await using var hold = new NpgsqlCommand("""
                SELECT 1 FROM tenant."FormWorkLeases" WHERE "TenantId" = @tenant AND "UserId" = @user FOR UPDATE
                """, holder, transaction);
            hold.Parameters.AddWithValue("tenant", tenantId);
            hold.Parameters.AddWithValue("user", person);
            Assert.Equal(1, await hold.ExecuteScalarAsync(Ct));

            // Lo que la operación ya guardó responde igual: soltar no lanza, avisa, y la barrera queda hasta vencer.
            await lease.DisposeAsync();
            await transaction.RollbackAsync(Ct);
        }

        Assert.Equal(FormCatalogImportErrors.BusyCode, Refused(await store.TryTakeAsync(person, FormWorkLeaseKind.Export, Ct)).Code);
        clock.Advance(FormWorkLease.Lifetime);
        await using var later = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Export, Ct));
    }

    [Fact]
    public async Task The_third_of_an_organization_gets_too_many_at_once_and_another_organization_does_not_wait()
    {
        var clock = Clock();
        var full = Store(await OrganizationAsync(), clock);
        var first = Taken(await full.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Preview, Ct));
        await using var second = Taken(await full.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Export, Ct));
        var third = Guid.NewGuid();

        var refused = Refused(await full.TryTakeAsync(third, FormWorkLeaseKind.Apply, Ct));
        Assert.Equal(FormCatalogImportErrors.TooManyAtOnceCode, refused.Code);
        Assert.Equal(ErrorType.TooManyRequests, refused.Type);
        Assert.Equal(FormWorkLeaseStore.RetryAfterSeconds, refused.Metadata!["retryAfter"]);
        await using (Taken(await Store(await OrganizationAsync(), clock).TryTakeAsync(third, FormWorkLeaseKind.Apply, Ct)))
        {
        }

        // La negada no dejó nada tomado: al terminar otra, entra.
        await first.DisposeAsync();
        await using var after = Taken(await full.TryTakeAsync(third, FormWorkLeaseKind.Apply, Ct));
    }

    [Fact]
    public async Task The_installation_limit_counts_every_organization()
    {
        var clock = Clock();
        var limits = new FormCatalogFileOptions { MaxPerOrganization = 2, MaxPerInstallation = 3 };
        var first = Store(await OrganizationAsync(), clock, limits);
        var leaving = Taken(await first.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Preview, Ct));
        await using var staying = Taken(await first.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Preview, Ct));
        await using var second = Taken(await Store(await OrganizationAsync(), clock, limits)
            .TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Export, Ct));

        var third = Store(await OrganizationAsync(), clock, limits);
        Assert.Equal(FormCatalogImportErrors.TooManyAtOnceCode,
            Refused(await third.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Apply, Ct)).Code);
        await leaving.DisposeAsync();
        await using var after = Taken(await third.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Apply, Ct));
    }

    [Fact]
    public async Task Two_people_asking_for_the_last_place_at_once_take_turns_and_only_one_gets_it()
    {
        var clock = Clock();
        var store = Store(await OrganizationAsync(), clock,
            new FormCatalogFileOptions { MaxPerOrganization = 1, MaxPerInstallation = 1_000 });

        // Mientras otra toma tiene el advisory lock de la barrera, las dos esperan; al soltarlo, la segunda ya cuenta a la primera.
        var (connection, transaction) = await FormsTestData.HoldLockAsync(factory, FormWorkLeaseStore.TakersLock);
        Task<Result<IAsyncDisposable>>[] takes;
        await using (connection)
        await using (transaction)
        {
            takes =
            [
                store.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Preview, Ct),
                store.TryTakeAsync(Guid.NewGuid(), FormWorkLeaseKind.Export, Ct),
            ];
            await FormsTestData.WaitForLockWaitersAsync(factory, FormWorkLeaseStore.TakersLock, 2);
            Assert.DoesNotContain(takes, take => take.IsCompleted);
            await transaction.CommitAsync(Ct);
        }

        var results = await Task.WhenAll(takes);
        await using var taken = Taken(Assert.Single(results, result => result.IsSuccess));
        Assert.Equal(FormCatalogImportErrors.TooManyAtOnceCode, Refused(Assert.Single(results, result => result.IsFailure)).Code);
    }

    // Un momento propio y lejano: las barreras de otras pruebas, con su reloj, ya vencieron o todavía no nacieron.
    private static FakeTimeProvider Clock() =>
        new(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(Random.Shared.NextInt64(0, 1_000_000_000)));

    private FormWorkLeaseStore Store(Guid tenantId, TimeProvider clock, FormCatalogFileOptions? limits = null,
        string? connectionString = null) =>
        new(connectionString ?? factory.RuntimeConnectionString, new Organization(tenantId), clock,
            Options.Create(new FormsOptions { CatalogFiles = limits ?? new FormCatalogFileOptions() }),
            NullLogger<FormWorkLeaseStore>.Instance);

    /// <summary>La barrera que dejó una copia que se cayó: está en la base y nadie la renueva ni la suelta.</summary>
    private Task LeaveAsync(Guid tenantId, Guid person, DateTime expiresAtUtc) => AdminAsync(tenantId, """
        INSERT INTO tenant."FormWorkLeases" ("Id", "TenantId", "UserId", "Kind", "ExpiresAtUtc")
        VALUES (@id, @tenant, @user, 'Apply', @expires)
        """, ("id", Guid.CreateVersion7()), ("tenant", tenantId), ("user", person), ("expires", expiresAtUtc));

    /// <summary>Una barrera que no se pudo renovar (la base no respondía) y venció.</summary>
    private Task ExpireAsync(Guid tenantId, Guid person, DateTime atUtc) => AdminAsync(tenantId, """
        UPDATE tenant."FormWorkLeases" SET "ExpiresAtUtc" = @at WHERE "TenantId" = @tenant AND "UserId" = @user
        """, ("at", atUtc), ("tenant", tenantId), ("user", person));

    private async Task<DateTime?> ExpiresAtAsync(Guid tenantId, Guid person)
    {
        await using var connection = await OpenAdminAsync(tenantId);
        await using var command = new NpgsqlCommand("""
            SELECT "ExpiresAtUtc" FROM tenant."FormWorkLeases" WHERE "TenantId" = @tenant AND "UserId" = @user
            """, connection);
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("user", person);
        return await command.ExecuteScalarAsync(Ct) as DateTime?;
    }

    private async Task AdminAsync(Guid tenantId, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAdminAsync(tenantId);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
    }

    private async Task<NpgsqlConnection> OpenAdminAsync(Guid tenantId)
    {
        var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection);
        enter.Parameters.AddWithValue("tenant", tenantId.ToString("D"));
        await enter.ExecuteNonQueryAsync(Ct);
        return connection;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!await condition()) await Task.Delay(20, timeout.Token);
    }

    private async Task<Guid> OrganizationAsync()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        return await OrganizationJourney.StageAsync(factory, owner, "Barrera " + Guid.NewGuid().ToString("N")[..8], Ct);
    }

    private static IAsyncDisposable Taken(Result<IAsyncDisposable> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private static Error Refused(Result<IAsyncDisposable> result)
    {
        Assert.True(result.IsFailure);
        return result.Error;
    }

    private sealed class Organization(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public TenantKind? TenantKind => Domain.Tenancy.TenantKind.Business;
        public Guid RequiredTenantId => tenantId;
    }
}
