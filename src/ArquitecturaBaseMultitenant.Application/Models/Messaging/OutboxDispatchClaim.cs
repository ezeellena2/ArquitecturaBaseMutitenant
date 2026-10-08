namespace ArquitecturaBaseMultitenant.Application.Models.Messaging;

/// <summary>Una reserva durable; el worker descifra únicamente después de confirmar el claim.</summary>
public sealed class OutboxDispatchClaim(Guid messageId, Guid leaseId, string channel, string encryptedPayload)
{
    public Guid MessageId { get; } = messageId;
    public Guid LeaseId { get; } = leaseId;
    public string Channel { get; } = channel;
    public string EncryptedPayload { get; } = encryptedPayload;
    public override string ToString() => $"OutboxDispatchClaim {MessageId} ({Channel})";
}
