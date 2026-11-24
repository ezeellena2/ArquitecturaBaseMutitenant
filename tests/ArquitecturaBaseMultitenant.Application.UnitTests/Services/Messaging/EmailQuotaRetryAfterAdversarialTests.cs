using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Messaging;

/// <summary>
/// Revisión adversarial de la B3: el <c>retryAfter</c> de un 429 dice cuándo vuelve a salir un código. Con dos topes llenos
/// a la vez (la dirección y el global de los códigos), la espera tiene que ser la del que libera lugar más tarde: si no,
/// la pantalla dice «probá en 22 h 30 min» y a esa hora vuelve a responder 429.
/// </summary>
public sealed class EmailQuotaRetryAfterAdversarialTests
{
    private static readonly LoginCodeDestination Destination =
        LoginCodeDestination.ForEmail(Email.Create("ana@example.test").Value);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Waiting_the_retry_after_of_a_rejected_code_lets_the_next_one_go_out()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 10, 30, 0, TimeSpan.Zero));
        var request = new RequestFixture();
        // Los códigos frenan en 2 (3 menos la reserva de 1); una dirección pide 1 por día.
        var guard = new EmailQuotaGuard(new MessageQuotaGuard(new FakeMessageQuotaRepository(), clock),
            new FakeMessageQuotaKeyHasher(), request,
            Options.Create(new EmailQuotaOptions { DailyLimit = 3, DailyInvitationReserve = 1, DailyCodeLimitPerAddress = 1 }),
            new FakeAfterCommit(), new FakeLogger<EmailQuotaGuard>(new FakeLogCollector()));

        request.IpAddress = "192.0.2.1";
        Assert.True((await guard.ReserveAsync(Destination, LoginCodePurpose.Login, null, Ct)).IsSuccess);
        clock.Advance(TimeSpan.FromHours(2));
        // Dos invitaciones llevan el global a 3, por encima de donde frenan los códigos.
        Assert.True((await guard.ReserveInvitationAsync(Guid.NewGuid(), Guid.NewGuid(), Ct)).IsSuccess);
        Assert.True((await guard.ReserveInvitationAsync(Guid.NewGuid(), Guid.NewGuid(), Ct)).IsSuccess);

        var rejected = await guard.ReserveAsync(Destination, LoginCodePurpose.Login, null, Ct);
        Assert.True(rejected.IsFailure);
        var retryAfter = (int)rejected.Error.Metadata![EmailQuotaErrors.RetryAfterKey]!;

        // Se espera lo que dijo el 429 y se vuelve a pedir desde la misma dirección.
        clock.Advance(TimeSpan.FromSeconds(retryAfter));
        var retried = await guard.ReserveAsync(Destination, LoginCodePurpose.Login, null, Ct);
        Assert.True(retried.IsSuccess,
            $"Después de esperar los {retryAfter} s del 429 volvió a responder {retried.Error.Code} con otros "
            + $"{retried.Error.Metadata?[EmailQuotaErrors.RetryAfterKey]} s de espera.");
    }

    private sealed class RequestFixture : IRequestInfo
    {
        public string? IpAddress { get; set; }

        public string? UserAgent => null;
    }
}
