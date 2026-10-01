using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Messaging;

/// <summary>
/// Comprueba el ciclo de un mensaje encolado, los reintentos y su caducidad operativa. Evita reenviar
/// mensajes enviados o exponer el payload en texto.
/// </summary>
public sealed class OutboxMessageTests
{
    private static readonly DateTime NowUtc = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Enqueued_message_is_due_and_does_not_expose_payload_in_text()
    {
        const string secret = "protected-code-123456";

        var message = OutboxMessage.Enqueue(OutboxChannel.Email, secret, NowUtc);

        Assert.Equal(OutboxChannel.Email, message.Channel);
        Assert.Equal(secret, message.EncryptedPayload);
        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(NowUtc, message.NextAttemptAtUtc);
        Assert.True(message.IsDue(NowUtc));
        Assert.DoesNotContain(secret, message.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Sent_message_is_not_dispatched_again()
    {
        var message = OutboxMessage.Enqueue(OutboxChannel.Email, "encrypted", NowUtc);

        message.MarkSent(NowUtc.AddMinutes(1));

        Assert.Equal(OutboxStatus.Sent, message.Status);
        Assert.Equal(NowUtc.AddMinutes(1), message.SentAtUtc);
        Assert.False(message.IsDue(NowUtc.AddHours(1)));
    }

    [Fact]
    public void Failed_message_retries_with_exponential_backoff_until_limit()
    {
        var message = OutboxMessage.Enqueue(OutboxChannel.Email, "encrypted", NowUtc);
        var delay = TimeSpan.FromMinutes(1);

        message.RecordFailure(NowUtc, delay, 3);
        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(NowUtc.AddMinutes(1), message.NextAttemptAtUtc);
        Assert.False(message.IsDue(NowUtc.AddSeconds(30)));
        Assert.True(message.IsDue(NowUtc.AddMinutes(1)));

        message.RecordFailure(NowUtc.AddMinutes(1), delay, 3);
        Assert.Equal(NowUtc.AddMinutes(3), message.NextAttemptAtUtc);

        message.RecordFailure(NowUtc.AddMinutes(3), delay, 3);
        Assert.Equal(3, message.Attempts);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.False(message.IsDue(NowUtc.AddYears(1)));
    }

    [Fact]
    public void Outbox_instants_must_be_utc()
    {
        var local = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => OutboxMessage.Enqueue(OutboxChannel.Email, "encrypted", local));
    }
}
