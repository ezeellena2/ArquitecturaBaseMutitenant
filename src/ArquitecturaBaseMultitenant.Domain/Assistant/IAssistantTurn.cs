namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Private state; the global work index never contains these events or snapshots.</summary>
public interface IAssistantTurn
{
    Guid Id { get; }
    Guid ConversationId { get; }
    Guid UserMessageId { get; }
    Guid AssistantMessageId { get; }
    Guid BindingId { get; }
    Guid[] ContextMessageIds { get; }
    void CaptureContext(IReadOnlyCollection<Guid> ids);
    string Status { get; }
    string EventsJson { get; }
    int Sequence { get; }
    uint? AdmissionVersion { get; }
    void CaptureAdmissionVersion(uint version);
    void Bind(Guid userMessageId, Guid assistantMessageId, Guid bindingId);
    void Checkpoint(string eventsJson, int sequence);
    void Finish(string status);
}
