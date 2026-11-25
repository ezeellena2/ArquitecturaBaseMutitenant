using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// Revisión adversarial de la B5, contra PostgreSQL real: un destino espera mientras tenga algo anterior «pendiente o en
/// curso» (ADR 0066 §6). Lo que venció su plazo no frena, salvo que ya esté saliendo: un envío en curso no se descarta.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class OutboxB5AdversarialReservationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Something_earlier_still_in_flight_holds_the_destination_even_after_its_discard_time()
    {
        using var client = factory.CreateClient();
        var channel = Channel("inflight-expired");
        var first = await EnqueueAsync(channel, "first",
            new OutboxEnqueueOptions("+5491100000009", DiscardAfterUtc: _clock.GetUtcNow().UtcDateTime.AddMinutes(1)));
        _clock.Advance(TimeSpan.FromSeconds(1));
        await EnqueueAsync(channel, "second", new OutboxEnqueueOptions("+5491100000009"));

        // El primero sale antes de su plazo y sigue en curso (la reserva dura 300 s).
        Assert.Equal(first, (await ClaimAsync(channel))!.MessageId);
        _clock.Advance(TimeSpan.FromMinutes(2));

        // Su plazo pasó mientras salía, pero sigue en curso: el segundo del mismo destino tiene que esperar.
        Assert.Null(await ClaimAsync(channel));
    }

    private static string Channel(string name)
    {
        var channel = $"{name}-{Guid.NewGuid():N}";
        return channel.Length > 40 ? channel[..40] : channel;
    }

    private async Task<Guid> EnqueueAsync(string channel, string payload, OutboxEnqueueOptions options)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var outbox = new Outbox(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPayloadProtector>(), _clock, [new Sender(channel)],
            scope.ServiceProvider.GetRequiredService<OutboxOrderingKeyHasher>());
        var id = Guid.Empty;
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            id = outbox.Enqueue(channel, payload, options: options);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return id;
    }

    private async Task<OutboxDispatchClaim?> ClaimAsync(string channel)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var dispatch = new OutboxDispatchService(provider.GetRequiredService<IUnitOfWork>(),
            new OutboxDispatchStore(provider.GetRequiredService<ApplicationDbContext>(), _clock, Options.Create(new OutboxOptions()),
                provider.GetRequiredService<IAfterCommit>(), provider.GetRequiredService<OutboxMetrics>()),
            [], _clock, NullLogger<OutboxDispatchService>.Instance);
        return (await dispatch.ClaimNextAsync([channel], Ct)).Value;
    }

    private sealed class Sender(string key) : IChannelSender
    {
        public string Key => key;
        public Task SendAsync(string payload, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The reservation tests do not send.");
    }
}
