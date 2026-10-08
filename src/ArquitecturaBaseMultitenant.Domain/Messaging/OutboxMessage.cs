using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>Conserva un envío cifrado hasta que el dispatcher lo confirme o agote sus reintentos; no realiza el transporte.</summary>
public sealed class OutboxMessage : Entity
{
    private OutboxMessage() { }

    public string Channel { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public string EncryptedPayload { get; private set; } = string.Empty;
    public OutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? LastErrorCode { get; private set; }

    public static OutboxMessage Enqueue(string channel, string encryptedPayload, DateTime nowUtc, Guid? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPayload);
        RequireUtc(nowUtc, nameof(nowUtc));

        return new OutboxMessage
        {
            Channel = channel,
            UserId = userId,
            EncryptedPayload = encryptedPayload,
            Status = OutboxStatus.Pending,
            CreatedAtUtc = nowUtc,
            NextAttemptAtUtc = nowUtc,
        };
    }

    public void Cancel()
    {
        if (Status == OutboxStatus.Sent) return;
        Status = OutboxStatus.Cancelled;
        NextAttemptAtUtc = null;
        EncryptedPayload = string.Empty;
        ClearLease();
    }

    public bool IsDue(DateTime nowUtc)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        return Status is OutboxStatus.Pending or OutboxStatus.Failed
            && NextAttemptAtUtc is { } dueAtUtc && nowUtc >= dueAtUtc
            && (LeaseExpiresAtUtc is null || LeaseExpiresAtUtc <= nowUtc);
    }

    /// <summary>Cada claim consume un intento, incluso si el proceso abandona el lease sin confirmar.</summary>
    public bool TryClaim(Guid leaseId, DateTime nowUtc, TimeSpan leaseDuration, int maxAttempts)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        if (leaseId == Guid.Empty) throw new ArgumentException("A lease is required.", nameof(leaseId));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        if (!IsDue(nowUtc)) return false;
        if (Attempts >= maxAttempts)
        {
            Status = OutboxStatus.Failed;
            LastErrorCode = "Outbox.Delivery.LeaseExpired";
            NextAttemptAtUtc = null;
            EncryptedPayload = string.Empty;
            ClearLease();
            return false;
        }
        Attempts++;
        LeaseId = leaseId;
        LeaseExpiresAtUtc = nowUtc + leaseDuration;
        return true;
    }

    /// <summary>Un lease vencido puede confirmar si nadie lo reclamó otra vez. Los resultados viejos no reviven filas.</summary>
    public bool TryComplete(Guid leaseId, bool accepted, bool retryable, string? providerMessageId,
        string? safeCode, DateTime nowUtc, TimeSpan baseDelay, int maxAttempts)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        if (LeaseId != leaseId || Status is OutboxStatus.Sent or OutboxStatus.Cancelled || NextAttemptAtUtc is null)
            return false;
        LastErrorCode = safeCode;
        if (accepted) { ProviderMessageId = providerMessageId; MarkSent(nowUtc); }
        else EndFailure(nowUtc, baseDelay, retryable ? maxAttempts : Attempts);
        ClearLease();
        return true;
    }

    public void MarkSent(DateTime nowUtc)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        if (Status == OutboxStatus.Sent || NextAttemptAtUtc is null)
        {
            throw new InvalidOperationException("The message cannot be sent in its current state.");
        }

        Status = OutboxStatus.Sent;
        SentAtUtc = nowUtc;
        NextAttemptAtUtc = null;
        EncryptedPayload = string.Empty;
        ClearLease();
    }

    public void RecordFailure(DateTime nowUtc, TimeSpan baseDelay, int maxAttempts)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        if (Status == OutboxStatus.Sent || NextAttemptAtUtc is null)
        {
            throw new InvalidOperationException("The message cannot fail in its current state.");
        }

        Attempts++;
        EndFailure(nowUtc, baseDelay, maxAttempts);
    }

    private void EndFailure(DateTime nowUtc, TimeSpan baseDelay, int maxAttempts)
    {
        Status = OutboxStatus.Failed;
        if (Attempts >= maxAttempts)
        {
            NextAttemptAtUtc = null;
            EncryptedPayload = string.Empty;
            return;
        }

        var delay = baseDelay;
        for (var attempt = 1; attempt < Attempts; attempt++)
        {
            delay *= 2;
        }

        NextAttemptAtUtc = nowUtc.Add(delay);
    }

    private void ClearLease() { LeaseId = null; LeaseExpiresAtUtc = null; }

    public override string ToString() => $"OutboxMessage {Id} ({Channel}, {Status})";

    private static void RequireUtc(DateTime instantUtc, string parameterName)
    {
        if (instantUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The instant must be UTC.", parameterName);
        }
    }
}
