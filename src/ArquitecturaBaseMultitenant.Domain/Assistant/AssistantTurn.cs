using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantTurn : Entity, ITenantOwned
{
    private AssistantTurn() { }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? LeaseId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }

    public static AssistantTurn Create(Guid conversationId, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A conversation is required.", nameof(conversationId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new AssistantTurn { ConversationId = conversationId, Status = "Pending", CreatedAtUtc = nowUtc };
    }
}
