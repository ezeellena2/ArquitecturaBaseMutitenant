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
    public uint Version { get; private set; }

    public static WhatsAppMessage Create(string phoneNumberId, string wamid, string eventType,
        WhatsAppMessageDirection direction, WhatsAppMessageKind kind, WhatsAppMessageStatus status,
        DateTime occurredAtUtc, DateTime receivedAtUtc, Guid? contactId = null,
        string? encryptedText = null, Guid? outboxMessageId = null)
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
            ReceivedAtUtc = receivedAtUtc, ContactId = contactId, EncryptedText = encryptedText, OutboxMessageId = outboxMessageId };
    }
    public bool TryLease(Guid leaseId, DateTime nowUtc, TimeSpan lifetime)
    {
        if (leaseId == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc || lifetime <= TimeSpan.Zero) throw new ArgumentException("Invalid lease.");
        if (ProcessedAtUtc is not null || LeaseUntilUtc > nowUtc) return false;
        LeaseId = leaseId; LeaseUntilUtc = nowUtc + lifetime; Attempts++; Status = WhatsAppMessageStatus.Processing;
        return true;
    }
    public bool Complete(Guid leaseId, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        if (LeaseId != leaseId || LeaseUntilUtc <= nowUtc) return false;
        ProcessedAtUtc = nowUtc; LeaseId = null; LeaseUntilUtc = null; Status = WhatsAppMessageStatus.Processed;
        return true;
    }
    public void ClearText() => EncryptedText = null;
    public void MarkOutboundAccepted(DateTime nowUtc)
    {
        if (Direction != WhatsAppMessageDirection.Outbound || nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Outbound UTC required.");
        ProcessedAtUtc = nowUtc;
    }
}
