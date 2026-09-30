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
    }

    public bool IsDue(DateTime nowUtc)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        return Status != OutboxStatus.Sent && NextAttemptAtUtc is { } dueAtUtc && nowUtc >= dueAtUtc;
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
        Status = OutboxStatus.Failed;
        if (Attempts >= maxAttempts)
        {
            NextAttemptAtUtc = null;
            return;
        }

        var delay = baseDelay;
        for (var attempt = 1; attempt < Attempts; attempt++)
        {
            delay *= 2;
        }

        NextAttemptAtUtc = nowUtc.Add(delay);
    }

    public override string ToString() => $"OutboxMessage {Id} ({Channel}, {Status})";

    private static void RequireUtc(DateTime instantUtc, string parameterName)
    {
        if (instantUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The instant must be UTC.", parameterName);
        }
    }
}
