using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public abstract class AssistantMessageBase : Entity, IVersioned
{
    public uint Version { get; private set; }
    public Guid ConversationId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public AssistantPersistedMessageStatus Status { get; private set; }
    public string? Text { get; private set; }
    public bool? PositiveFeedback { get; private set; }
    public string? ToolKey { get; private set; }
    public long? ToolDurationMilliseconds { get; private set; }
    public AssistantPersistedToolState? ToolState { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? LinksJson { get; private set; }
    public Guid[] SubjectUserIds { get; private set; } = [];
    public bool PrivacySubjectsKnown { get; private set; }
    public void SetPrivacySubjects(IReadOnlyCollection<Guid> subjects)
    {
        SubjectUserIds = subjects.Where(id => id != Guid.Empty).Distinct().Order().ToArray();
        PrivacySubjectsKnown = true;
    }
    public void AddPrivacySubjects(IReadOnlyCollection<Guid> subjects)
    {
        if (!PrivacySubjectsKnown) throw new InvalidOperationException("Unknown message provenance cannot be upgraded by a later tool.");
        SetPrivacySubjects(SubjectUserIds.Concat(subjects).ToArray());
    }

    public void SetLinks(string linksJson) { ArgumentException.ThrowIfNullOrWhiteSpace(linksJson); LinksJson = linksJson; }
    public void Checkpoint(string text) { Text = text; }
    public void Finish(string status)
    {
        var state = Enum.Parse<AssistantPersistedMessageStatus>(status);
        if (!Enum.IsDefined(state)) throw new ArgumentException("A defined status is required.", nameof(status));
        Status = state;
    }
    public void SetFeedback(bool? positive)
    {
        PositiveFeedback = positive;
    }

    protected void Initialize(Guid conversationId, string role, string status, string? text, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A conversation is required.", nameof(conversationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        ConversationId = conversationId; Role = role; Finish(status); Text = text; CreatedAtUtc = nowUtc;
    }
}
