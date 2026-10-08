using ArquitecturaBaseMultitenant.Domain.Common;
namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Entrada durable y metadatos Meta; Authentication nunca conserva contenido, OTP o enlaces.</summary>
public sealed class WhatsAppMessage : Entity, IVersioned
{
    private WhatsAppMessage() { PhoneNumberId = string.Empty; Wamid = string.Empty; EventType = string.Empty; }
    public string PhoneNumberId { get; private set; }
    public string Wamid { get; private set; }
    public string EventType { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? OutboxMessageId { get; private set; }
    public Guid? OriginalUserId { get; private set; }
    public Guid? OriginalMethodId { get; private set; }
    public string? ReplyId { get; private set; }
    public WhatsAppMessageDirection Direction { get; private set; }
    public WhatsAppMessageKind Kind { get; private set; }
    public WhatsAppMessageStatus Status { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? EncryptedText { get; private set; }
    public int Attempts { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseUntilUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public uint Version { get; private set; }

    public static WhatsAppMessage Create(string phoneNumberId, string wamid, string eventType,
        WhatsAppMessageDirection direction, WhatsAppMessageKind kind, WhatsAppMessageStatus status,
        DateTime occurredAtUtc, DateTime receivedAtUtc, Guid? contactId = null,
        string? encryptedText = null, Guid? outboxMessageId = null, Guid? originalUserId = null,
        Guid? originalMethodId = null, string? replyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(wamid);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        if (occurredAtUtc.Kind != DateTimeKind.Utc || receivedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("UTC required.");
        if (kind == WhatsAppMessageKind.Authentication && encryptedText is not null)
            throw new ArgumentException("Authentication messages contain metadata only.", nameof(encryptedText));
        return new WhatsAppMessage { PhoneNumberId = phoneNumberId, Wamid = wamid, EventType = eventType,
            Direction = direction, Kind = kind, Status = status, OccurredAtUtc = occurredAtUtc,
            ReceivedAtUtc = receivedAtUtc, ContactId = contactId, EncryptedText = encryptedText, OutboxMessageId = outboxMessageId,
            OriginalUserId = originalUserId, OriginalMethodId = originalMethodId, ReplyId = replyId,
            NextAttemptAtUtc = direction == WhatsAppMessageDirection.Inbound ? receivedAtUtc : null };
    }
    public bool TryLease(Guid leaseId, DateTime nowUtc, TimeSpan lifetime, int maxAttempts = 8)
    {
        if (leaseId == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc || lifetime <= TimeSpan.Zero) throw new ArgumentException("Invalid lease.");
        if (Direction != WhatsAppMessageDirection.Inbound || EventType != "inbound"
            || ProcessedAtUtc is not null || NextAttemptAtUtc is null || NextAttemptAtUtc > nowUtc || LeaseUntilUtc > nowUtc) return false;
        if (Attempts >= maxAttempts) { Status = WhatsAppMessageStatus.Failed; NextAttemptAtUtc = null; LeaseId = null; LeaseUntilUtc = null; return false; }
        LeaseId = leaseId; LeaseUntilUtc = nowUtc + lifetime; Attempts++; Status = WhatsAppMessageStatus.Processing;
        return true;
    }
    public bool Complete(Guid leaseId, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        if (LeaseId != leaseId || LeaseUntilUtc <= nowUtc) return false;
        ProcessedAtUtc = nowUtc; LeaseId = null; LeaseUntilUtc = null; NextAttemptAtUtc = null; Status = WhatsAppMessageStatus.Processed;
        return true;
    }
    public bool Fail(Guid leaseId, DateTime nowUtc, TimeSpan retryDelay, int maxAttempts)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || retryDelay <= TimeSpan.Zero || maxAttempts < 1) throw new ArgumentException("Invalid retry.");
        if (LeaseId != leaseId || ProcessedAtUtc is not null) return false;
        Status = WhatsAppMessageStatus.Failed; LeaseId = null; LeaseUntilUtc = null;
        NextAttemptAtUtc = Attempts < maxAttempts ? nowUtc + retryDelay * Math.Min(Attempts, maxAttempts) : null;
        return true;
    }
    public bool ApplyDeliveryStatus(WhatsAppMessageStatus status, DateTime occurredAtUtc)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.");
        if (Direction != WhatsAppMessageDirection.Outbound) return false;
        var current = DeliveryRank(Status); var next = DeliveryRank(status);
        if (next < 0 || next < current || (next == current && occurredAtUtc <= OccurredAtUtc)) return false;
        Status = status; OccurredAtUtc = occurredAtUtc;
        return true;
    }
    private static int DeliveryRank(WhatsAppMessageStatus status) => status switch
    { WhatsAppMessageStatus.Sent => 0, WhatsAppMessageStatus.Failed => 1, WhatsAppMessageStatus.Delivered => 2, WhatsAppMessageStatus.Read => 3, _ => -1 };
    public void ClearText() => EncryptedText = null;
    public void MarkOutboundAccepted(DateTime nowUtc)
    {
        if (Direction != WhatsAppMessageDirection.Outbound || nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Outbound UTC required.");
        ProcessedAtUtc = nowUtc;
    }
}
