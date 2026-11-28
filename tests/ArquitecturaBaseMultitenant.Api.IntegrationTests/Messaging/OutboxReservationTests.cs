using System.Diagnostics.Metrics;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// La reserva de la cola contra PostgreSQL real (ADR 0066): lo que contesta sale antes que los avisos, un destino espera
/// mientras tenga algo anterior pendiente o en curso, el destino se guarda solo como HMAC y un código que vence en la cola
/// se descarta sin salir, y lo cuenta el medidor.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class OutboxReservationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private readonly OutboxFairnessCursor _fairness = new();

    [Fact]
    public async Task A_reply_goes_before_an_earlier_notice()
    {
        using var client = factory.CreateClient();
        var channel = Channel("priority");
        var notice = await EnqueueAsync(channel, "notice", new OutboxEnqueueOptions());
        _clock.Advance(TimeSpan.FromSeconds(1));
        var reply = await EnqueueAsync(channel, "reply", new OutboxEnqueueOptions(Priority: OutboxPriority.Reply));

        Assert.Equal(reply, (await ClaimAsync(channel))!.MessageId);
        Assert.Equal(notice, (await ClaimAsync(channel))!.MessageId);
    }

    [Fact]
    public async Task A_destination_waits_while_something_earlier_is_pending_or_in_flight()
    {
        using var client = factory.CreateClient();
        var channel = Channel("ordering");
        var first = await EnqueueAsync(channel, "first", new OutboxEnqueueOptions("+5491100000001"));
        _clock.Advance(TimeSpan.FromSeconds(1));
        var second = await EnqueueAsync(channel, "second", new OutboxEnqueueOptions("+5491100000001", OutboxPriority.Reply));
        _clock.Advance(TimeSpan.FromSeconds(1));
        var other = await EnqueueAsync(channel, "other", new OutboxEnqueueOptions("+5491100000002"));

        // El segundo es una respuesta, pero el primero del mismo destino sale antes; otro destino no espera.
        var claimedFirst = (await ClaimAsync(channel))!;
        Assert.Equal(first, claimedFirst.MessageId);
        Assert.Equal(other, (await ClaimAsync(channel))!.MessageId);
        Assert.Null(await ClaimAsync(channel));

        // No salió y espera su reintento: el destino sigue esperando.
        await CompleteAsync(claimedFirst, OutboxDeliveryReceipt.Rejected("Test.Transport.NotSent", retryable: true));
        Assert.Null(await ClaimAsync(channel));
        _clock.Advance(TimeSpan.FromSeconds(30));
        var retried = (await ClaimAsync(channel))!;
        Assert.Equal(first, retried.MessageId);

        // «Sin confirmar» ya terminó: no frena lo que sigue.
        await CompleteAsync(retried, OutboxDeliveryReceipt.Uncertain("Test.Transport.Uncertain"));
        Assert.Equal(second, (await ClaimAsync(channel))!.MessageId);
    }

    [Fact]
    public async Task Something_earlier_that_already_expired_does_not_hold_the_destination()
    {
        using var client = factory.CreateClient();
        var channel = Channel("expired-ordering");
        var expired = await EnqueueAsync(channel, "old code",
            new OutboxEnqueueOptions("ana@example.test", DiscardAfterUtc: _clock.GetUtcNow().UtcDateTime.AddMinutes(1)));
        _clock.Advance(TimeSpan.FromSeconds(1));
        var later = await EnqueueAsync(channel, "new code", new OutboxEnqueueOptions("ana@example.test"));
        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(later, (await ClaimAsync(channel))!.MessageId);
        var saved = await ReadAsync(expired);
        Assert.Equal(OutboxStatus.Cancelled, saved.Status);
        Assert.Equal(OutboxMessage.ExpiredCode, saved.LastErrorCode);
    }

    [Fact]
    public async Task The_destination_is_kept_only_as_its_hmac()
    {
        using var client = factory.CreateClient();
        var channel = Channel("hmac");
        const string destination = "+5491134567890";
        var id = await EnqueueAsync(channel, "payload", new OutboxEnqueueOptions(destination));
        var otherChannel = await EnqueueAsync(Channel("hmac-other"), "payload", new OutboxEnqueueOptions(destination));

        var saved = await ReadAsync(id);
        Assert.NotNull(saved.OrderingKey);
        Assert.Equal(OutboxMessage.OrderingKeyMaxLength, saved.OrderingKey.Length);
        Assert.DoesNotContain("34567890", saved.OrderingKey, StringComparison.Ordinal);
        Assert.Equal(OutboxPriority.Notice, saved.Priority);
        Assert.Null(saved.DiscardAfterUtc);
        // El canal va dentro del HMAC: dos canales no comparten la espera.
        Assert.NotEqual(saved.OrderingKey, (await ReadAsync(otherChannel)).OrderingKey);
    }

    [Fact]
    public async Task An_email_code_that_expires_in_the_queue_is_discarded_and_counted()
    {
        using var client = factory.CreateClient();
        await factory.Services.SeedDatabaseAsync(Ct);
        var discarded = new List<string>();
        var isolated = Channel("code-expired");
        using var listener = Listen(OutboxMetrics.DiscardedName, discarded);
        Guid id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var channel = new EmailLoginCodeChannel(services.GetRequiredService<IEmailTemplateRenderer>(), CreateOutbox(services, OutboxChannel.Email), _clock);
            var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
            var context = services.GetRequiredService<ApplicationDbContext>();
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await channel.EnqueueVerificationAsync(null, $"vence-{Guid.NewGuid():N}@example.test", "123456", 10, profile, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            id = Assert.Single(context.OutboxMessages.Local).Id;
            // La cola de correo es compartida con los demás tests: el código pasa a un canal propio para reservarlo solo.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE platform."OutboxMessages" SET "Channel" = {isolated} WHERE "Id" = {id}""", Ct);
        }
        var saved = await ReadAsync(id);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(10), saved.DiscardAfterUtc);
        Assert.Equal(OutboxPriority.Reply, saved.Priority);
        _clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(id, (await ClaimAsync(isolated))!.MessageId);
        await ResetLeaseAsync(id);

        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Null(await ClaimAsync(isolated));

        saved = await ReadAsync(id);
        Assert.Equal(OutboxStatus.Cancelled, saved.Status);
        Assert.Equal(OutboxMessage.ExpiredCode, saved.LastErrorCode);
        Assert.Equal(0, saved.Attempts);
        Assert.Empty(saved.EncryptedPayload);
        Assert.Single(discarded, value => value == isolated);
    }

    [Fact]
    public async Task Email_codes_go_ahead_of_two_hundred_queued_broadcast_notices()
    {
        using var client = factory.CreateClient();
        await factory.Services.SeedDatabaseAsync(Ct);
        var isolated = Channel("email-code-ahead");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var outbox = CreateOutbox(services, isolated);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                for (var index = 0; index < 200; index++)
                    outbox.Enqueue(isolated, $"broadcast-{index}", options: new OutboxEnqueueOptions(Priority: OutboxPriority.Notice));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        _clock.Advance(TimeSpan.FromSeconds(1));

        Guid[] codeIds;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var channel = new EmailLoginCodeChannel(services.GetRequiredService<IEmailTemplateRenderer>(), CreateOutbox(services, OutboxChannel.Email), _clock);
            var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
            var context = services.GetRequiredService<ApplicationDbContext>();
            var renderedLogin = await channel.RenderLoginCodeAsync($"ingreso-{Guid.NewGuid():N}@example.test", "111111", 10, profile, Ct);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                channel.EnqueueRenderedLoginCode(renderedLogin, 10);
                await channel.EnqueueSignupAsync($"alta-{Guid.NewGuid():N}@example.test", "222222", 10, profile, ct);
                await channel.EnqueueVerificationAsync(null, $"verif-{Guid.NewGuid():N}@example.test", "333333", 10, profile, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            codeIds = [.. context.OutboxMessages.Local.Select(message => message.Id)];
            Assert.Equal(3, codeIds.Length);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE platform."OutboxMessages" SET "Channel" = {isolated} WHERE "Id" = ANY({codeIds})""", Ct);
        }

        foreach (var id in codeIds) Assert.Equal(OutboxPriority.Reply, (await ReadAsync(id)).Priority);
        var firstThree = new[]
        {
            (await ClaimAsync(isolated))!.MessageId,
            (await ClaimAsync(isolated))!.MessageId,
            (await ClaimAsync(isolated))!.MessageId,
        };
        Assert.Equal(codeIds.Order(), firstThree.Order());
    }

    [Fact]
    public async Task Fair_dispatch_delivers_a_small_organization_within_the_first_ten_ahead_of_a_large_broadcast_and_keeps_replies_first()
    {
        using var client = factory.CreateClient();
        var channel = Channel("fairness");
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var outbox = CreateOutbox(services, channel);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                for (var index = 0; index < 200; index++)
                    outbox.Enqueue(channel, $"broadcast-a-{index}",
                        options: new OutboxEnqueueOptions(Priority: OutboxPriority.Notice, TenantId: tenantA));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        _clock.Advance(TimeSpan.FromSeconds(1));

        var tenantBIds = new Guid[2];
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var outbox = CreateOutbox(services, channel);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                tenantBIds[0] = outbox.Enqueue(channel, "notice-b-0",
                    options: new OutboxEnqueueOptions(Priority: OutboxPriority.Notice, TenantId: tenantB));
                tenantBIds[1] = outbox.Enqueue(channel, "notice-b-1",
                    options: new OutboxEnqueueOptions(Priority: OutboxPriority.Notice, TenantId: tenantB));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        _clock.Advance(TimeSpan.FromSeconds(1));
        var replyId = await EnqueueAsync(channel, "code-reply",
            new OutboxEnqueueOptions(Priority: OutboxPriority.Reply));

        var firstTen = new Guid[10];
        for (var index = 0; index < firstTen.Length; index++)
            firstTen[index] = (await ClaimAsync(channel))!.MessageId;

        Assert.Equal(replyId, firstTen[0]);
        Assert.All(tenantBIds, id => Assert.Contains(id, firstTen));
    }

    [Fact]
    public async Task Sent_messages_are_counted_by_channel_only_after_the_commit()
    {
        using var client = factory.CreateClient();
        var channel = Channel("sent-metric");
        var sent = new List<string>();
        using var listener = Listen(OutboxMetrics.SentName, sent);
        await EnqueueAsync(channel, "payload", new OutboxEnqueueOptions());
        var claim = (await ClaimAsync(channel))!;
        Assert.DoesNotContain(channel, sent);

        await CompleteAsync(claim, OutboxDeliveryReceipt.Accepted());
        await CompleteAsync(claim, OutboxDeliveryReceipt.Accepted());

        Assert.Single(sent, value => value == channel);
    }

    private static string Channel(string name)
    {
        var channel = $"{name}-{Guid.NewGuid():N}";
        return channel.Length > 40 ? channel[..40] : channel;
    }

    private async Task<Guid> EnqueueAsync(string channel, string payload, OutboxEnqueueOptions options)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var outbox = CreateOutbox(scope.ServiceProvider, channel);
        var id = Guid.Empty;
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            id = outbox.Enqueue(channel, payload, options: options);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return id;
    }

    private Outbox CreateOutbox(IServiceProvider services, string channel) => new(services.GetRequiredService<ApplicationDbContext>(),
        services.GetRequiredService<IPayloadProtector>(), _clock, [new Sender(channel)], services.GetRequiredService<OutboxOrderingKeyHasher>());

    private async Task<OutboxDispatchClaim?> ClaimAsync(string channel)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await Dispatch(scope.ServiceProvider).ClaimNextAsync([channel], Ct)).Value;
    }

    private async Task CompleteAsync(OutboxDispatchClaim claim, OutboxDeliveryReceipt receipt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True((await Dispatch(scope.ServiceProvider).CompleteAsync(claim.MessageId, claim.LeaseId, receipt, Ct)).IsSuccess);
    }

    /// <summary>Devuelve la reserva como el apagado, sin gastar el intento.</summary>
    private async Task ResetLeaseAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var message = await ReadAsync(id);
        Assert.True((await Dispatch(scope.ServiceProvider).ReleaseAsync(id, message.LeaseId!.Value, Ct)).IsSuccess);
    }

    private async Task<OutboxMessage> ReadAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .SingleAsync(value => value.Id == id, Ct);
    }

    private OutboxDispatchService Dispatch(IServiceProvider provider) =>
        new(provider.GetRequiredService<IUnitOfWork>(), new OutboxDispatchStore(provider.GetRequiredService<ApplicationDbContext>(),
            _clock, Options.Create(new OutboxOptions()), provider.GetRequiredService<IAfterCommit>(),
            provider.GetRequiredService<OutboxMetrics>(), _fairness), [], _clock, NullLogger<OutboxDispatchService>.Instance);

    /// <summary>Escucha un contador del medidor de la cola y anota el canal de cada medición.</summary>
    private static MeterListener Listen(string instrument, List<string> channels)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (published, meterListener) =>
            {
                if (published.Meter.Name == OutboxMetrics.MeterName && published.Name == instrument)
                    meterListener.EnableMeasurementEvents(published);
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "channel" && tag.Value is string channel)
                    lock (channels) channels.Add(channel);
        });
        listener.Start();
        return listener;
    }

    private sealed class Sender(string key) : IChannelSender
    {
        public string Key => key;
        public Task SendAsync(string payload, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The reservation tests do not send.");
    }
}
