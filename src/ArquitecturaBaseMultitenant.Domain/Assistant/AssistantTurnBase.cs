using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public abstract class AssistantTurnBase : Entity, IAssistantTurn
{
    public Guid ConversationId { get; private set; }
    public Guid? LeaseId { get; private set; }
    public AssistantTurnStatus Status { get; private set; }
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
    public void Finish(string status)
    {
        var state = Enum.Parse<AssistantTurnStatus>(status);
        if (!Enum.IsDefined(state)) throw new ArgumentException("A defined status is required.", nameof(status));
        Status = state;
    }
    protected void Initialize(Guid conversationId, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A conversation is required.", nameof(conversationId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        ConversationId = conversationId; Status = AssistantTurnStatus.Pending; CreatedAtUtc = nowUtc;
    }
}
