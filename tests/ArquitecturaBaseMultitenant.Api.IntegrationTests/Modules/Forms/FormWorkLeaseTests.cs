using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Configuration;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// La barrera de las operaciones de archivo de los catálogos (C11) con la base real: una por persona, que se toma sin esperar y se
/// suelta al terminar; una vencida se retoma; otra persona no espera; la tercera de una organización da <c>TooManyAtOnce</c> y otra
/// organización no espera; el tope de la instalación cuenta todas las organizaciones. Cada prueba corre en su propio momento, lejos
/// del reloj de las demás, así las barreras de otras pruebas no cuentan.
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
        var store = Store(await OrganizationAsync(), clock);
        var person = Guid.NewGuid();
        var stale = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Apply, Ct));

        clock.Advance(FormWorkLease.Lifetime - TimeSpan.FromSeconds(1));
        Assert.Equal(FormCatalogImportErrors.BusyCode, Refused(await store.TryTakeAsync(person, FormWorkLeaseKind.Preview, Ct)).Code);

        // El proceso que la tenía se cayó: a los dos minutos vence sola y la persona vuelve a tomarla.
        clock.Advance(TimeSpan.FromSeconds(1));
        await using var current = Taken(await store.TryTakeAsync(person, FormWorkLeaseKind.Preview, Ct));

        // Si el de antes termina tarde, suelta la suya, no la nueva.
        await stale.DisposeAsync();
        Assert.Equal(FormCatalogImportErrors.BusyCode, Refused(await store.TryTakeAsync(person, FormWorkLeaseKind.Export, Ct)).Code);
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

    private FormWorkLeaseStore Store(Guid tenantId, TimeProvider clock, FormCatalogFileOptions? limits = null) =>
        new(factory.RuntimeConnectionString, new Organization(tenantId), clock,
            Options.Create(new FormsOptions { CatalogFiles = limits ?? new FormCatalogFileOptions() }));

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
