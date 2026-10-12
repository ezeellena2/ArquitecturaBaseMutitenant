using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantTurn : Entity, ITenantOwned, IAssistantTurn
{
    private AssistantTurn() { }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? LeaseId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }

    public Guid UserMessageId { get; private set; }
    public Guid AssistantMessageId { get; private set; }
    public Guid BindingId { get; private set; }
    public Guid[] ContextMessageIds { get; private set; } = [];
    public void CaptureContext(IReadOnlyCollection<Guid> ids) => ContextMessageIds = ids.Distinct().ToArray();
    public string EventsJson { get; private set; } = "[]";
    public int Sequence { get; private set; }
    public uint? AdmissionVersion { get; private set; }
    public void CaptureAdmissionVersion(uint version) => AdmissionVersion ??= version;
    public void Bind(Guid userMessageId, Guid assistantMessageId, Guid bindingId)
    {
        UserMessageId = userMessageId; AssistantMessageId = assistantMessageId; BindingId = bindingId;
    }
    public void Checkpoint(string eventsJson, int sequence) { EventsJson = eventsJson; Sequence = sequence; }
    public void Finish(string status) => Status = status;
    public static AssistantTurn Create(Guid conversationId, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A conversation is required.", nameof(conversationId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new AssistantTurn { ConversationId = conversationId, Status = "Pending", CreatedAtUtc = nowUtc };
    }
}
