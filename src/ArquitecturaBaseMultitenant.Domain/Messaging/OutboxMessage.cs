using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>
/// Conserva un envío cifrado hasta que el dispatcher lo confirme, agote sus reintentos, venza o quede «Sin confirmar»
/// (ADR 0066); no realiza el transporte. Lo que no se sabe si salió nunca se vuelve a reclamar.
/// </summary>
public sealed class OutboxMessage : Entity
{
    /// <summary>La reserva venció sin respuesta: el transporte pudo haber salido.</summary>
    public const string InterruptedCode = "Outbox.Delivery.Interrupted";

    /// <summary>Venció su plazo (<see cref="DiscardAfterUtc"/>) antes de salir.</summary>
    public const string ExpiredCode = "Outbox.Delivery.Expired";

    /// <summary>La versión que lo reclamó ya no le da intentos (un tope más bajo que el de quien lo reintentó).</summary>
    public const string ExhaustedCode = "Outbox.Delivery.LeaseExpired";

    /// <summary>Un HMAC-SHA256 en hexadecimal.</summary>
    public const int OrderingKeyMaxLength = 64;

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

    /// <summary>El HMAC del destino que da el canal: lo anterior del mismo destino sale primero. Nunca el destino en claro.</summary>
    public string? OrderingKey { get; private set; }

    /// <summary>Lo que contesta sale antes que los avisos (ADR 0066 §6).</summary>
    public OutboxPriority Priority { get; private set; } = OutboxPriority.Notice;

    /// <summary>Desde este instante no se manda: se descarta con <see cref="ExpiredCode"/>, como un código que ya venció.</summary>
    public DateTime? DiscardAfterUtc { get; private set; }

    public static OutboxMessage Enqueue(string channel, string encryptedPayload, DateTime nowUtc, Guid? userId = null,
        string? orderingKey = null, OutboxPriority priority = OutboxPriority.Notice, DateTime? discardAfterUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPayload);
        RequireUtc(nowUtc, nameof(nowUtc));
        if (discardAfterUtc is { } discardAt) RequireUtc(discardAt, nameof(discardAfterUtc));
        if (orderingKey is not null && (orderingKey.Length is 0 or > OrderingKeyMaxLength))
            throw new ArgumentException("The ordering key must be a short technical hash.", nameof(orderingKey));
        if (!Enum.IsDefined(priority)) throw new ArgumentOutOfRangeException(nameof(priority));

        return new OutboxMessage
        {
            Channel = channel,
            UserId = userId,
            EncryptedPayload = encryptedPayload,
            Status = OutboxStatus.Pending,
            CreatedAtUtc = nowUtc,
            NextAttemptAtUtc = nowUtc,
            OrderingKey = orderingKey,
            Priority = priority,
            DiscardAfterUtc = discardAfterUtc,
        };
    }

    /// <summary>Lo que ya terminó (enviado o «Sin confirmar») no se cancela: no queda nada por mandar.</summary>
    public void Cancel()
    {
        if (Status is OutboxStatus.Sent or OutboxStatus.Uncertain) return;
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

    /// <summary>
    /// Cada reserva consume un intento. Una reserva anterior que venció sin respuesta deja el mensaje «Sin confirmar»: pudo
    /// haber salido, así que no se vuelve a reclamar. Lo que venció su plazo se descarta sin salir.
    /// </summary>
    public OutboxClaimResult TryClaim(Guid leaseId, DateTime nowUtc, TimeSpan leaseDuration, int maxAttempts)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        if (leaseId == Guid.Empty) throw new ArgumentException("A lease is required.", nameof(leaseId));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        if (!IsDue(nowUtc)) return OutboxClaimResult.NotDue;
        if (LeaseId is not null)
        {
            // IsDue garantiza que la reserva venció: el intento anterior pudo haber llegado al transporte.
            MarkUncertain(InterruptedCode);
            return OutboxClaimResult.Interrupted;
        }
        if (DiscardAfterUtc is { } discardAtUtc && discardAtUtc <= nowUtc)
        {
            Status = OutboxStatus.Cancelled;
            LastErrorCode = ExpiredCode;
            NextAttemptAtUtc = null;
            EncryptedPayload = string.Empty;
            return OutboxClaimResult.Expired;
        }
        if (Attempts >= maxAttempts)
        {
            Status = OutboxStatus.Failed;
            LastErrorCode = ExhaustedCode;
            NextAttemptAtUtc = null;
            EncryptedPayload = string.Empty;
            return OutboxClaimResult.Exhausted;
        }
        Attempts++;
        LeaseId = leaseId;
        LeaseExpiresAtUtc = nowUtc + leaseDuration;
        return OutboxClaimResult.Claimed;
    }

    /// <summary>
    /// Devuelve la reserva sin haber llamado al transporte (el apagado llegó antes): vuelve a pendiente, le toca enseguida a
    /// otra copia y no gasta el intento.
    /// </summary>
    public bool TryRelease(Guid leaseId)
    {
        if (LeaseId is null || LeaseId != leaseId || Status is not (OutboxStatus.Pending or OutboxStatus.Failed)
            || NextAttemptAtUtc is null) return false;
        Status = OutboxStatus.Pending;
        Attempts = Math.Max(0, Attempts - 1);
        ClearLease();
        return true;
    }

    /// <summary>
    /// Un lease vencido puede confirmar si nadie lo reclamó otra vez. Los resultados viejos no reviven filas: lo enviado, lo
    /// cancelado y lo «Sin confirmar» ya terminaron.
    /// </summary>
    public bool TryComplete(Guid leaseId, OutboxAttemptOutcome outcome, string? providerMessageId,
        string? safeCode, DateTime nowUtc, TimeSpan baseDelay, int maxAttempts)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        if (LeaseId is null || LeaseId != leaseId || Status is not (OutboxStatus.Pending or OutboxStatus.Failed)
            || NextAttemptAtUtc is null)
            return false;
        switch (outcome)
        {
            case OutboxAttemptOutcome.Accepted:
                LastErrorCode = null;
                ProviderMessageId = providerMessageId;
                MarkSent(nowUtc);
                return true;
            case OutboxAttemptOutcome.Uncertain:
                MarkUncertain(safeCode);
                return true;
            default:
                LastErrorCode = safeCode;
                EndFailure(nowUtc, baseDelay, outcome == OutboxAttemptOutcome.Retry ? maxAttempts : Attempts);
                ClearLease();
                return true;
        }
    }

    public void MarkSent(DateTime nowUtc)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        if (Status is not (OutboxStatus.Pending or OutboxStatus.Failed) || NextAttemptAtUtc is null)
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
        if (Status is not (OutboxStatus.Pending or OutboxStatus.Failed) || NextAttemptAtUtc is null)
        {
            throw new InvalidOperationException("The message cannot fail in its current state.");
        }

        Attempts++;
        EndFailure(nowUtc, baseDelay, maxAttempts);
    }

    /// <summary>Mientras le queden intentos vuelve a pendiente con su espera, que se duplica; sin intentos, no salió.</summary>
    private void EndFailure(DateTime nowUtc, TimeSpan baseDelay, int maxAttempts)
    {
        if (Attempts >= maxAttempts)
        {
            Status = OutboxStatus.Failed;
            NextAttemptAtUtc = null;
            EncryptedPayload = string.Empty;
            return;
        }

        var delay = baseDelay;
        for (var attempt = 1; attempt < Attempts; attempt++)
        {
            delay *= 2;
        }

        Status = OutboxStatus.Pending;
        NextAttemptAtUtc = nowUtc.Add(delay);
    }

    private void MarkUncertain(string? safeCode)
    {
        Status = OutboxStatus.Uncertain;
        LastErrorCode = safeCode;
        NextAttemptAtUtc = null;
        EncryptedPayload = string.Empty;
        ClearLease();
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
