using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Messaging;

/// <summary>
/// ADR 0063 y decisión 256: los topes de correo en las 24 horas móviles. Los códigos frenan en el tope menos la reserva y
/// las invitaciones llegan al tope; cada dirección, cuenta, persona que invita y organización tiene el suyo, y el global es
/// de todos. Un rechazo dice cuánto esperar y no suma en ningún contador.
/// </summary>
public sealed class EmailQuotaGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 30, 0, TimeSpan.Zero);
    private static readonly LoginCodeDestination Destination = LoginCodeDestination.ForEmail(Email.Create("ana@example.test").Value);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Codes_stop_at_the_limit_minus_the_reserve_and_invitations_reach_the_limit()
    {
        var f = new Fixture(new EmailQuotaOptions { DailyLimit = 5, DailyInvitationReserve = 2 });

        for (var index = 0; index < 3; index++) Assert.True((await f.CodeAsync("192.0.2." + index)).IsSuccess);
        var code = await f.CodeAsync("192.0.2.99");
        AssertLimited(code, (24 * 60 + 30) * 60);
        Assert.True((await f.InviteAsync(Guid.NewGuid(), Guid.NewGuid(), Ct)).IsSuccess);
        Assert.True((await f.InviteAsync(Guid.NewGuid(), Guid.NewGuid(), Ct)).IsSuccess);
        AssertLimited(await f.InviteAsync(Guid.NewGuid(), Guid.NewGuid(), Ct), (24 * 60 + 30) * 60);
        Assert.Equal(5, f.Quotas.Total(MessageQuotaScope.Global));
    }

    [Fact]
    public async Task An_address_counts_its_codes_sent_or_withheld_and_a_rejection_records_nothing()
    {
        var f = new Fixture(new EmailQuotaOptions { DailyCodeLimitPerAddress = 2 });

        Assert.True((await f.CodeAsync("192.0.2.1")).IsSuccess);
        Assert.True((await f.WithheldAsync("192.0.2.1")).IsSuccess);
        AssertLimited(await f.CodeAsync("192.0.2.1"), (24 * 60 + 30) * 60);
        AssertLimited(await f.WithheldAsync("192.0.2.1"), (24 * 60 + 30) * 60);
        // Lo que no salió no gasta el global; el rechazo no suma en ninguno.
        Assert.Equal(1, f.Quotas.Total(MessageQuotaScope.Global));
        Assert.Equal(2, f.Quotas.Total(MessageQuotaScope.CodeIp));
        // Otra dirección tiene el suyo.
        Assert.True((await f.CodeAsync("192.0.2.2")).IsSuccess);
    }

    [Fact]
    public async Task A_withheld_code_answers_the_same_429_as_a_sent_one_without_spending_the_global_or_the_account()
    {
        var f = new Fixture(new EmailQuotaOptions
        {
            DailyLimit = 3, DailyInvitationReserve = 1, DailyCodeLimitPerAccount = 1,
        });
        var account = Guid.NewGuid();

        // Uno que no sale cuenta en su dirección y no en la cuenta ni en el global.
        Assert.True((await f.WithheldAsync("192.0.2.1", LoginCodePurpose.VerifyDestination, account)).IsSuccess);
        Assert.Equal(0, f.Quotas.Total(MessageQuotaScope.Global));
        Assert.Equal(0, f.Quotas.Total(MessageQuotaScope.CodeAccount));
        // Con la cuenta llena, responde lo mismo que uno que sale.
        Assert.True((await f.CodeAsync("192.0.2.2", LoginCodePurpose.VerifyDestination, account)).IsSuccess);
        AssertLimited(await f.WithheldAsync("192.0.2.3", LoginCodePurpose.VerifyDestination, account), (24 * 60 + 30) * 60);
        // Con los lugares de los códigos llenos (3 menos la reserva de 1), también.
        Assert.True((await f.CodeAsync("192.0.2.4")).IsSuccess);
        AssertLimited(await f.CodeAsync("192.0.2.5"), (24 * 60 + 30) * 60);
        AssertLimited(await f.WithheldAsync("192.0.2.6"), (24 * 60 + 30) * 60);
        Assert.Equal(2, f.Quotas.Total(MessageQuotaScope.Global));
        Assert.Equal(3, f.Quotas.Total(MessageQuotaScope.CodeIp));
    }

    [Fact]
    public async Task The_warning_waits_for_the_commit_and_a_rollback_drops_it()
    {
        var f = new Fixture(new EmailQuotaOptions { DailyLimit = 2, DailyInvitationReserve = 0 }, commit: false);

        Assert.True((await f.CodeAsync("192.0.2.1")).IsSuccess);
        Assert.True((await f.CodeAsync("192.0.2.2")).IsSuccess);
        // El que llega al tope deja su aviso para el commit: antes no hay aviso, y un rollback lo descarta.
        Assert.Equal(1, f.AfterCommit.PendingCount);
        Assert.Empty(f.Warnings());
        f.AfterCommit.Rollback();
        f.AfterCommit.Commit();
        Assert.Empty(f.Warnings());
    }

    [Fact]
    public async Task From_my_account_each_account_has_its_limit_and_signing_in_does_not_count_it()
    {
        var f = new Fixture(new EmailQuotaOptions { DailyCodeLimitPerAccount = 1 });
        var account = Guid.NewGuid();

        Assert.True((await f.CodeAsync("192.0.2.1", LoginCodePurpose.VerifyDestination, account)).IsSuccess);
        AssertLimited(await f.CodeAsync("192.0.2.2", LoginCodePurpose.VerifyDestination, account), (24 * 60 + 30) * 60);
        Assert.True((await f.CodeAsync("192.0.2.3", LoginCodePurpose.Login, account)).IsSuccess);
        Assert.True((await f.CodeAsync("192.0.2.4", LoginCodePurpose.VerifyDestination, Guid.NewGuid())).IsSuccess);
        Assert.Equal(2, f.Quotas.Total(MessageQuotaScope.CodeAccount));
    }

    [Fact]
    public async Task Each_organization_and_each_inviter_has_its_limit_and_the_global_is_shared()
    {
        var f = new Fixture(new EmailQuotaOptions
        {
            DailyLimit = 5, DailyInvitationReserve = 0, DailyInvitationLimitPerInviter = 3,
            DailyInvitationLimitPerOrganization = 2,
        });
        var (first, second, inviter) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.True((await f.InviteAsync(inviter, first, Ct)).IsSuccess);
        Assert.True((await f.InviteAsync(Guid.NewGuid(), first, Ct)).IsSuccess);
        AssertLimited(await f.InviteAsync(Guid.NewGuid(), first, Ct), (24 * 60 + 30) * 60);
        // La otra organización tiene el suyo; quien invita suma en las dos.
        Assert.True((await f.InviteAsync(inviter, second, Ct)).IsSuccess);
        Assert.True((await f.InviteAsync(inviter, null, Ct)).IsSuccess);
        AssertLimited(await f.InviteAsync(inviter, Guid.NewGuid(), Ct), (24 * 60 + 30) * 60);
        // El global es de todos: con 4 de 5, un código ajeno a las dos organizaciones llena el día.
        Assert.True((await f.CodeAsync("192.0.2.1")).IsSuccess);
        AssertLimited(await f.InviteAsync(Guid.NewGuid(), Guid.NewGuid(), Ct), (24 * 60 + 30) * 60);
        Assert.Equal(5, f.Quotas.Total(MessageQuotaScope.Global));
    }

    [Fact]
    public async Task The_window_moves_by_hours()
    {
        var f = new Fixture(new EmailQuotaOptions { DailyLimit = 2, DailyInvitationReserve = 0 });

        Assert.True((await f.CodeAsync("192.0.2.1")).IsSuccess);
        f.Clock.Advance(TimeSpan.FromHours(2));
        Assert.True((await f.CodeAsync("192.0.2.1")).IsSuccess);
        // Lleno: libera un lugar cuando sale la hora de las 10, mañana a las 11 (22 h 30 min después de las 12:30).
        AssertLimited(await f.CodeAsync("192.0.2.1"), (22 * 60 + 30) * 60);
        f.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 25, 11, 0, 1, TimeSpan.Zero));
        Assert.True((await f.CodeAsync("192.0.2.1")).IsSuccess);
    }

    [Fact]
    public async Task The_log_warns_once_at_80_percent_when_codes_stop_and_at_the_limit()
    {
        var f = new Fixture(new EmailQuotaOptions { DailyLimit = 10, DailyInvitationReserve = 2 });

        for (var index = 0; index < 8; index++) Assert.True((await f.CodeAsync("192.0.2." + index)).IsSuccess);
        Assert.Equal(2, f.Warnings().Count); // el 80 % y el freno de los códigos, los dos en 8
        AssertLimited(await f.CodeAsync("192.0.2.50"), (24 * 60 + 30) * 60);
        Assert.Equal(2, f.Warnings().Count);
        Assert.True((await f.InviteAsync(Guid.NewGuid(), Guid.NewGuid(), Ct)).IsSuccess);
        Assert.True((await f.InviteAsync(Guid.NewGuid(), Guid.NewGuid(), Ct)).IsSuccess);
        Assert.Equal(3, f.Warnings().Count);
        AssertLimited(await f.InviteAsync(Guid.NewGuid(), Guid.NewGuid(), Ct), (24 * 60 + 30) * 60);
        Assert.Equal(3, f.Warnings().Count);
        Assert.DoesNotContain(f.Logs.GetSnapshot(), record => record.Message.Contains("192.0.2", StringComparison.Ordinal));
    }

    private static void AssertLimited(Result result, int retryAfterSeconds)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(EmailQuotaErrors.DailyLimitCode, result.Error.Code);
        Assert.Equal(retryAfterSeconds, result.Error.Metadata![EmailQuotaErrors.RetryAfterKey]);
    }

    private sealed class Fixture
    {
        private readonly bool _commit;

        public Fixture(EmailQuotaOptions options, bool commit = true)
        {
            _commit = commit;
            Guard = new EmailQuotaGuard(new MessageQuotaGuard(Quotas, Clock), new FakeMessageQuotaKeyHasher(), Request,
                Options.Create(options), AfterCommit, new FakeLogger<EmailQuotaGuard>(Logs));
        }

        public FakeAfterCommit AfterCommit { get; } = new();

        public FakeTimeProvider Clock { get; } = new(Now);

        public FakeMessageQuotaRepository Quotas { get; } = new();

        public FakeLogCollector Logs { get; } = new();

        public RequestFixture Request { get; } = new();

        public EmailQuotaGuard Guard { get; }

        public Task<Result> CodeAsync(string ip, LoginCodePurpose purpose = LoginCodePurpose.Login, Guid? owner = null)
        {
            Request.IpAddress = ip;
            return Committed(Guard.ReserveAsync(Destination, purpose, owner, Ct));
        }

        public Task<Result> WithheldAsync(string ip, LoginCodePurpose purpose = LoginCodePurpose.Login, Guid? owner = null)
        {
            Request.IpAddress = ip;
            return Committed(Guard.ReserveWithheldAsync(Destination, purpose, owner, Ct));
        }

        public Task<Result> InviteAsync(Guid inviter, Guid? organization, CancellationToken ct) =>
            Committed(Guard.ReserveInvitationAsync(inviter, organization, ct));

        // Cada pedido es su transacción: se confirma apenas termina, salvo en los tests del commit.
        private async Task<Result> Committed(Task<Result> reserved)
        {
            var result = await reserved;
            if (_commit) AfterCommit.Commit();
            return result;
        }

        public IReadOnlyList<FakeLogRecord> Warnings() =>
            [.. Logs.GetSnapshot().Where(record => record.Level == LogLevel.Warning)];
    }

    private sealed class RequestFixture : IRequestInfo
    {
        public string? IpAddress { get; set; }

        public string? UserAgent => null;
    }
}
