using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class PlatformAssistantMessage : Entity, IVersioned
{
    private PlatformAssistantMessage() { }
    public uint Version { get; private set; }
    public Guid ConversationId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? Text { get; private set; }
    public bool? PositiveFeedback { get; private set; }
    public string? ToolKey { get; private set; }
    public long? ToolDurationMilliseconds { get; private set; }
    public string? ToolState { get; private set; }
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
    public void Finish(string status) { Status = status; }
    public void SetFeedback(bool? positive)
    {
        PositiveFeedback = positive;
    }

    public static PlatformAssistantMessage Create(Guid conversationId, string role, string status, string? text, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A conversation is required.", nameof(conversationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new PlatformAssistantMessage { ConversationId = conversationId, Role = role, Status = status, Text = text, CreatedAtUtc = nowUtc };
    }
}
