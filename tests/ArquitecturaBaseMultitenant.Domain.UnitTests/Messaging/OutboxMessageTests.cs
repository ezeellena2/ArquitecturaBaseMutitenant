using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Messaging;

/// <summary>
/// Comprueba el ciclo de un mensaje encolado, los reintentos, lo «Sin confirmar» y lo que vence (ADR 0066). Evita reenviar
/// mensajes enviados o dudosos, o exponer el payload en texto.
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
        Assert.Equal(string.Empty, message.EncryptedPayload);
    }

    [Fact]
    public void Failed_message_retries_with_exponential_backoff_until_limit()
    {
        var message = OutboxMessage.Enqueue(OutboxChannel.Email, "encrypted", NowUtc);
        var delay = TimeSpan.FromMinutes(1);

        message.RecordFailure(NowUtc, delay, 3);
        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(NowUtc.AddMinutes(1), message.NextAttemptAtUtc);
        Assert.False(message.IsDue(NowUtc.AddSeconds(30)));
        Assert.True(message.IsDue(NowUtc.AddMinutes(1)));

        message.RecordFailure(NowUtc.AddMinutes(1), delay, 3);
        Assert.Equal(NowUtc.AddMinutes(3), message.NextAttemptAtUtc);

        message.RecordFailure(NowUtc.AddMinutes(3), delay, 3);
        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.Equal(3, message.Attempts);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.False(message.IsDue(NowUtc.AddYears(1)));
        Assert.Equal(string.Empty, message.EncryptedPayload);
    }

    [Fact]
    public void An_abandoned_lease_becomes_uncertain_and_is_never_claimed_again()
    {
        var message = OutboxMessage.Enqueue("email", "encrypted", NowUtc);
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(first, NowUtc, TimeSpan.FromMinutes(5), 3));
        Assert.Equal(OutboxClaimResult.NotDue, message.TryClaim(second, NowUtc.AddMinutes(1), TimeSpan.FromMinutes(5), 3));

        Assert.Equal(OutboxClaimResult.Interrupted, message.TryClaim(second, NowUtc.AddMinutes(5), TimeSpan.FromMinutes(5), 3));

        Assert.Equal(OutboxStatus.Uncertain, message.Status);
        Assert.Equal(OutboxMessage.InterruptedCode, message.LastErrorCode);
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Null(message.LeaseId);
        Assert.Empty(message.EncryptedPayload);
        Assert.False(message.IsDue(NowUtc.AddYears(1)));
        Assert.Equal(OutboxClaimResult.NotDue, message.TryClaim(Guid.NewGuid(), NowUtc.AddYears(1), TimeSpan.FromMinutes(5), 3));
        // El resultado tardío del intento abandonado no lo revive ni lo da por enviado.
        Assert.False(message.TryComplete(first, OutboxAttemptOutcome.Accepted, "late", null, NowUtc.AddMinutes(6), TimeSpan.FromSeconds(30), 3));
        Assert.Equal(OutboxStatus.Uncertain, message.Status);
        // Cancelar (la baja de la cuenta) no cambia lo que ya terminó.
        message.Cancel();
        Assert.Equal(OutboxStatus.Uncertain, message.Status);
    }

    [Fact]
    public void Old_completion_cannot_resurrect_a_sent_message()
    {
        var message = OutboxMessage.Enqueue("email", "encrypted", NowUtc);
        var lease = Guid.NewGuid();
        Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(lease, NowUtc, TimeSpan.FromMinutes(5), 3));
        Assert.False(message.TryComplete(Guid.NewGuid(), OutboxAttemptOutcome.Accepted, "other", null, NowUtc, TimeSpan.FromSeconds(30), 3));
        Assert.True(message.TryComplete(lease, OutboxAttemptOutcome.Accepted, "receipt", null, NowUtc.AddMinutes(1), TimeSpan.FromSeconds(30), 3));
        Assert.Equal("receipt", message.ProviderMessageId);
        Assert.False(message.TryComplete(lease, OutboxAttemptOutcome.Retry, null, "Transport.Failed", NowUtc.AddMinutes(2), TimeSpan.FromSeconds(30), 3));
        Assert.Equal(OutboxStatus.Sent, message.Status);
        Assert.Empty(message.EncryptedPayload);
    }

    [Theory]
    [InlineData(OutboxAttemptOutcome.Retry, OutboxStatus.Pending)]
    [InlineData(OutboxAttemptOutcome.Rejected, OutboxStatus.Failed)]
    [InlineData(OutboxAttemptOutcome.Uncertain, OutboxStatus.Uncertain)]
    public void Each_outcome_of_an_attempt_has_its_transition(OutboxAttemptOutcome outcome, OutboxStatus expected)
    {
        var message = OutboxMessage.Enqueue("email", "encrypted", NowUtc);
        var lease = Guid.NewGuid();
        Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(lease, NowUtc, TimeSpan.FromMinutes(5), 3));

        Assert.True(message.TryComplete(lease, outcome, null, "Email.Smtp.Code", NowUtc.AddSeconds(10), TimeSpan.FromSeconds(30), 3));

        Assert.Equal(expected, message.Status);
        Assert.Equal("Email.Smtp.Code", message.LastErrorCode);
        Assert.Null(message.LeaseId);
        if (expected == OutboxStatus.Pending)
        {
            Assert.Equal(NowUtc.AddSeconds(40), message.NextAttemptAtUtc);
            Assert.Equal("encrypted", message.EncryptedPayload);
            Assert.Equal(OutboxClaimResult.NotDue, message.TryClaim(Guid.NewGuid(), NowUtc.AddSeconds(39), TimeSpan.FromMinutes(5), 3));
            Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(Guid.NewGuid(), NowUtc.AddSeconds(40), TimeSpan.FromMinutes(5), 3));
        }
        else
        {
            Assert.Null(message.NextAttemptAtUtc);
            Assert.Empty(message.EncryptedPayload);
            Assert.False(message.IsDue(NowUtc.AddYears(1)));
        }
    }

    [Fact]
    public void A_retry_on_the_last_attempt_fails()
    {
        var message = OutboxMessage.Enqueue("email", "encrypted", NowUtc);
        var lease = Guid.NewGuid();
        Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(lease, NowUtc, TimeSpan.FromMinutes(5), 1));
        Assert.True(message.TryComplete(lease, OutboxAttemptOutcome.Retry, null, "Email.Smtp.NotSent", NowUtc, TimeSpan.FromSeconds(30), 1));
        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Empty(message.EncryptedPayload);
    }

    [Fact]
    public void Release_gives_the_claim_back_without_spending_the_attempt()
    {
        var message = OutboxMessage.Enqueue("email", "encrypted", NowUtc);
        var lease = Guid.NewGuid();
        Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(lease, NowUtc, TimeSpan.FromMinutes(5), 3));
        Assert.False(message.TryRelease(Guid.NewGuid()));

        Assert.True(message.TryRelease(lease));

        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.LeaseId);
        Assert.True(message.IsDue(NowUtc));
        Assert.False(message.TryRelease(lease));
        Assert.False(message.TryComplete(lease, OutboxAttemptOutcome.Accepted, "late", null, NowUtc, TimeSpan.FromSeconds(30), 3));
    }

    [Fact]
    public void A_deferred_attempt_waits_its_turn_without_spending_the_attempt()
    {
        var message = OutboxMessage.Enqueue("whatsapp", "encrypted", NowUtc);
        var lease = Guid.NewGuid();
        Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(lease, NowUtc, TimeSpan.FromMinutes(5), 1));

        Assert.True(message.TryComplete(lease, OutboxAttemptOutcome.Deferred, null, "WhatsApp.Broadcast.ModuleOff", NowUtc,
            TimeSpan.FromSeconds(30), 1));

        Assert.Equal((OutboxStatus.Pending, 0, (Guid?)null), (message.Status, message.Attempts, message.LeaseId));
        Assert.Equal("encrypted", message.EncryptedPayload);
        Assert.False(message.IsDue(NowUtc.AddSeconds(29)));
        Assert.True(message.IsDue(NowUtc.AddSeconds(30)));
        // Con un solo intento, postergarlo no lo agota: la reserva siguiente todavía puede mandarlo.
        Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(Guid.NewGuid(), NowUtc.AddSeconds(30), TimeSpan.FromMinutes(5), 1));
    }

    [Fact]
    public void Withdraw_takes_out_only_what_is_waiting_without_a_claim()
    {
        var waiting = OutboxMessage.Enqueue("whatsapp", "encrypted", NowUtc);
        Assert.True(waiting.TryWithdraw());
        Assert.Equal((OutboxStatus.Cancelled, OutboxMessage.WithdrawnCode, (DateTime?)null), (waiting.Status, waiting.LastErrorCode, waiting.NextAttemptAtUtc));
        Assert.Empty(waiting.EncryptedPayload);
        Assert.False(waiting.TryWithdraw());

        // Lo reservado pudo haber llegado al transporte, y lo enviado ya terminó.
        var claimed = OutboxMessage.Enqueue("whatsapp", "encrypted", NowUtc);
        Assert.Equal(OutboxClaimResult.Claimed, claimed.TryClaim(Guid.NewGuid(), NowUtc, TimeSpan.FromMinutes(5), 3));
        Assert.False(claimed.TryWithdraw());
        Assert.Equal(OutboxStatus.Pending, claimed.Status);
        var sent = OutboxMessage.Enqueue("whatsapp", "encrypted", NowUtc);
        sent.MarkSent(NowUtc);
        Assert.False(sent.TryWithdraw());
        Assert.Equal(OutboxStatus.Sent, sent.Status);

        // Lo que espera un reintento todavía no salió.
        var retrying = OutboxMessage.Enqueue("whatsapp", "encrypted", NowUtc);
        retrying.RecordFailure(NowUtc, TimeSpan.FromSeconds(30), 3);
        Assert.True(retrying.TryWithdraw());
    }

    [Fact]
    public void Expired_message_is_discarded_without_being_claimed()
    {
        var message = OutboxMessage.Enqueue("email", "encrypted", NowUtc, discardAfterUtc: NowUtc.AddMinutes(10));
        Assert.Equal(OutboxClaimResult.Expired, message.TryClaim(Guid.NewGuid(), NowUtc.AddMinutes(10), TimeSpan.FromMinutes(5), 3));
        Assert.Equal(OutboxStatus.Cancelled, message.Status);
        Assert.Equal(OutboxMessage.ExpiredCode, message.LastErrorCode);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Empty(message.EncryptedPayload);

        var fresh = OutboxMessage.Enqueue("email", "encrypted", NowUtc, discardAfterUtc: NowUtc.AddMinutes(10));
        Assert.Equal(OutboxClaimResult.Claimed, fresh.TryClaim(Guid.NewGuid(), NowUtc.AddMinutes(9), TimeSpan.FromMinutes(5), 3));
    }

    [Fact]
    public void Enqueue_keeps_ordering_priority_and_deadline()
    {
        var key = new string('a', OutboxMessage.OrderingKeyMaxLength);
        var message = OutboxMessage.Enqueue("whatsapp", "encrypted", NowUtc, null, key, OutboxPriority.Reply, NowUtc.AddMinutes(5));
        Assert.Equal(key, message.OrderingKey);
        Assert.Equal(OutboxPriority.Reply, message.Priority);
        Assert.Equal(NowUtc.AddMinutes(5), message.DiscardAfterUtc);
        Assert.Equal(OutboxPriority.Notice, OutboxMessage.Enqueue("email", "encrypted", NowUtc).Priority);
        Assert.Throws<ArgumentException>(() => OutboxMessage.Enqueue("email", "encrypted", NowUtc, orderingKey: key + "a"));
        Assert.Throws<ArgumentException>(() => OutboxMessage.Enqueue("email", "encrypted", NowUtc,
            discardAfterUtc: new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Local)));
    }

    [Fact]
    public void Cancel_and_exhausted_attempts_clear_payload_and_prevent_completion()
    {
        var message = OutboxMessage.Enqueue("email", "encrypted", NowUtc);
        var lease = Guid.NewGuid();
        Assert.Equal(OutboxClaimResult.Claimed, message.TryClaim(lease, NowUtc, TimeSpan.FromMinutes(5), 1));
        message.Cancel();
        Assert.False(message.TryComplete(lease, OutboxAttemptOutcome.Accepted, "receipt", null, NowUtc.AddMinutes(1), TimeSpan.FromSeconds(30), 1));
        Assert.Equal(OutboxStatus.Cancelled, message.Status);
        Assert.Empty(message.EncryptedPayload);
        // Una versión con un tope más bajo no le da otro intento.
        var retried = OutboxMessage.Enqueue("email", "encrypted", NowUtc);
        retried.RecordFailure(NowUtc, TimeSpan.FromSeconds(30), 5);
        Assert.Equal(OutboxClaimResult.Exhausted, retried.TryClaim(Guid.NewGuid(), NowUtc.AddMinutes(1), TimeSpan.FromMinutes(5), 1));
        Assert.Equal(OutboxStatus.Failed, retried.Status);
        Assert.Null(retried.NextAttemptAtUtc);
        Assert.Empty(retried.EncryptedPayload);
    }

    [Fact]
    public void Outbox_instants_must_be_utc()
    {
        var local = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => OutboxMessage.Enqueue(OutboxChannel.Email, "encrypted", local));
    }
}
