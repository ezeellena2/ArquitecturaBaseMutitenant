using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

/// <summary>
/// Decisión 1 de los arreglos de la revisión (T05-03 / B9): los topes diarios de invitaciones por WhatsApp de «Cargar
/// teléfonos», por organización y global de la plataforma, en las 24 horas móviles. Si la carga entera no entra, responde
/// 429 <c>WhatsApp.PhoneImport.DailyLimit</c> con la espera en <c>retryAfter</c> y no suma nada en ningún contador.
/// </summary>
public sealed class WhatsAppPhoneImportQuotaGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 30, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_organization_has_its_own_limit_and_the_global_is_shared()
    {
        var f = new Fixture(new WhatsAppQuotaOptions
        {
            DailyInvitationLimitPerOrganization = 3,
            DailyInvitationLimit = 5,
        });
        var (first, second, third) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.True((await f.ReserveAsync(first, 2)).IsSuccess);
        // La carga de 2 no entra en el 1 que le queda a la organización: se rechaza entera y no suma nada.
        AssertLimited(await f.ReserveAsync(first, 2), (24 * 60 + 30) * 60);
        Assert.Equal(2, f.Quotas.Total(MessageQuotaScope.WhatsAppInvitationOrganization));
        Assert.Equal(2, f.Quotas.Total(MessageQuotaScope.WhatsAppInvitationGlobal));

        // 1 más sí entra en la primera organización; luego llena su tope propio.
        Assert.True((await f.ReserveAsync(first, 1)).IsSuccess);
        AssertLimited(await f.ReserveAsync(first, 1), (24 * 60 + 30) * 60);

        // Otra organización sigue entrando hasta completar el global compartido (3 + 2 = 5).
        Assert.True((await f.ReserveAsync(second, 2)).IsSuccess);
        AssertLimited(await f.ReserveAsync(third, 1), (24 * 60 + 30) * 60);
        Assert.Equal(5, f.Quotas.Total(MessageQuotaScope.WhatsAppInvitationOrganization));
        Assert.Equal(5, f.Quotas.Total(MessageQuotaScope.WhatsAppInvitationGlobal));
    }

    [Fact]
    public async Task A_batch_that_does_not_fit_records_nothing_and_returns_when_enough_places_open()
    {
        var f = new Fixture(new WhatsAppQuotaOptions
        {
            DailyInvitationLimitPerOrganization = 6,
            DailyInvitationLimit = 10,
        });
        var org = Guid.NewGuid();

        // 10:30 → 2 invitaciones (salen mañana a las 11:00).
        Assert.True((await f.ReserveAsync(org, 2)).IsSuccess);
        // 13:30 → 3 invitaciones (salen mañana a las 14:00). Total = 5, queda 1 lugar.
        f.Clock.Advance(TimeSpan.FromHours(3));
        Assert.True((await f.ReserveAsync(org, 3)).IsSuccess);

        // Pedir 2 ahora (13:30): necesita que salgan los 2 de las 10:00 (mañana a las 11:00 = 21 h 30 min).
        AssertLimited(await f.ReserveAsync(org, 2), (21 * 60 + 30) * 60);
        // Pedir 4 ahora (13:30): con 2 libres quedan 3, 3 + 4 = 7 > 6; necesita que salgan también los 3 de las 13:00 (mañana a las 14:00 = 24 h 30 min).
        AssertLimited(await f.ReserveAsync(org, 4), (24 * 60 + 30) * 60);
        // Pedir 7 (más que el tope diario de la organización): no falla con excepción, espera la ventana completa (24 h 30 min).
        AssertLimited(await f.ReserveAsync(org, 7), (24 * 60 + 30) * 60);

        // Ningún rechazo sumó nada.
        Assert.Equal(5, f.Quotas.Total(MessageQuotaScope.WhatsAppInvitationOrganization));
        Assert.Equal(5, f.Quotas.Total(MessageQuotaScope.WhatsAppInvitationGlobal));
    }

    [Fact]
    public async Task When_both_limits_are_exceeded_retry_after_is_the_later_one()
    {
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());

        // Caso 1: el tope de la organización libera más tarde (24 h 30 min) que el global (22 h 30 min).
        var orgLater = new Fixture(new WhatsAppQuotaOptions
        {
            DailyInvitationLimitPerOrganization = 2,
            DailyInvitationLimit = 3,
        });
        Assert.True((await orgLater.ReserveAsync(first, 1)).IsSuccess);
        orgLater.Clock.Advance(TimeSpan.FromHours(2));
        Assert.True((await orgLater.ReserveAsync(second, 2)).IsSuccess);
        AssertLimited(await orgLater.ReserveAsync(second, 1), (24 * 60 + 30) * 60);

        // Caso 2: el global libera más tarde (24 h 30 min) que la organización (22 h 30 min).
        var globalLater = new Fixture(new WhatsAppQuotaOptions
        {
            DailyInvitationLimitPerOrganization = 2,
            DailyInvitationLimit = 2,
        });
        Assert.True((await globalLater.ReserveAsync(first, 1)).IsSuccess);
        globalLater.Clock.Advance(TimeSpan.FromHours(2));
        Assert.True((await globalLater.ReserveAsync(second, 1)).IsSuccess);
        AssertLimited(await globalLater.ReserveAsync(first, 2), (24 * 60 + 30) * 60);
    }

    [Fact]
    public async Task The_window_moves_by_hours_and_frees_exact_batch_capacity()
    {
        var f = new Fixture(new WhatsAppQuotaOptions
        {
            DailyInvitationLimitPerOrganization = 4,
            DailyInvitationLimit = 10,
        });
        var org = Guid.NewGuid();

        Assert.True((await f.ReserveAsync(org, 2)).IsSuccess);
        f.Clock.Advance(TimeSpan.FromHours(2));
        Assert.True((await f.ReserveAsync(org, 2)).IsSuccess);
        // Lleno (4/4): un lote de 2 libera lugar cuando sale la hora de las 10:00, mañana a las 11:00 (22 h 30 min).
        AssertLimited(await f.ReserveAsync(org, 2), (22 * 60 + 30) * 60);

        f.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 25, 11, 0, 1, TimeSpan.Zero));
        // Ahora quedan 2 lugares libres: un lote de 3 todavía no entra (espera a que salga la hora de las 12:00 = 1 h 59 min 59 s).
        AssertLimited(await f.ReserveAsync(org, 3), 2 * 3600 - 1);
        // Un lote de 2 sí entra.
        Assert.True((await f.ReserveAsync(org, 2)).IsSuccess);
    }

    [Fact]
    public async Task The_log_warns_once_at_80_percent_and_at_the_limit_after_commit_without_leaking_the_organization()
    {
        var f = new Fixture(new WhatsAppQuotaOptions
        {
            DailyInvitationLimitPerOrganization = 10,
            DailyInvitationLimit = 10,
        });
        var org = Guid.NewGuid();

        // 5 + 4 = 9 cruza el 80 % (8) en un lote sin llegar al 100 %: un aviso.
        Assert.True((await f.ReserveAsync(org, 5)).IsSuccess);
        Assert.Empty(f.Warnings());
        Assert.True((await f.ReserveAsync(org, 4)).IsSuccess);
        Assert.Single(f.Warnings());

        // Llegar a 10 cruza el 100 %: segundo aviso; un rechazo posterior no repite avisos.
        Assert.True((await f.ReserveAsync(org, 1)).IsSuccess);
        Assert.Equal(2, f.Warnings().Count);
        AssertLimited(await f.ReserveAsync(org, 1), (24 * 60 + 30) * 60);
        Assert.Equal(2, f.Warnings().Count);
        Assert.DoesNotContain(f.Logs.GetSnapshot(),
            record => record.Message.Contains(org.ToString(), StringComparison.OrdinalIgnoreCase));

        // Sin commit, un rollback descarta el aviso pendiente.
        var rollbackFixture = new Fixture(new WhatsAppQuotaOptions
        {
            DailyInvitationLimitPerOrganization = 2,
            DailyInvitationLimit = 2,
        }, commit: false);
        Assert.True((await rollbackFixture.ReserveAsync(org, 2)).IsSuccess);
        Assert.Equal(1, rollbackFixture.AfterCommit.PendingCount);
        Assert.Empty(rollbackFixture.Warnings());
        rollbackFixture.AfterCommit.Rollback();
        rollbackFixture.AfterCommit.Commit();
        Assert.Empty(rollbackFixture.Warnings());
    }

    [Fact]
    public async Task An_empty_batch_succeeds_without_locking_or_recording()
    {
        var f = new Fixture(new WhatsAppQuotaOptions
        {
            DailyInvitationLimitPerOrganization = 1,
            DailyInvitationLimit = 1,
        });
        var org = Guid.NewGuid();

        Assert.True((await f.ReserveAsync(org, 0)).IsSuccess);
        Assert.Empty(f.Quotas.Locks);
        Assert.Empty(f.Quotas.Counts);
    }

    private static void AssertLimited(Result result, int retryAfterSeconds)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(WhatsAppErrors.PhoneImportDailyLimitCode, result.Error.Code);
        Assert.Equal(retryAfterSeconds, result.Error.Metadata![WhatsAppErrors.RetryAfterKey]);
    }

    private sealed class Fixture
    {
        private readonly bool _commit;

        public Fixture(WhatsAppQuotaOptions options, bool commit = true)
        {
            _commit = commit;
            Guard = new WhatsAppPhoneImportQuotaGuard(new MessageQuotaGuard(Quotas, Clock), new FakeMessageQuotaKeyHasher(),
                Options.Create(options), AfterCommit, new FakeLogger<WhatsAppPhoneImportQuotaGuard>(Logs));
        }

        public FakeAfterCommit AfterCommit { get; } = new();

        public FakeTimeProvider Clock { get; } = new(Now);

        public FakeMessageQuotaRepository Quotas { get; } = new();

        public FakeLogCollector Logs { get; } = new();

        public WhatsAppPhoneImportQuotaGuard Guard { get; }

        public async Task<Result> ReserveAsync(Guid organizationId, int count)
        {
            var result = await Guard.ReserveAsync(organizationId, count, Ct);
            if (_commit) AfterCommit.Commit();
            return result;
        }

        public IReadOnlyList<FakeLogRecord> Warnings() =>
            [.. Logs.GetSnapshot().Where(record => record.Level == LogLevel.Warning)];
    }
}
