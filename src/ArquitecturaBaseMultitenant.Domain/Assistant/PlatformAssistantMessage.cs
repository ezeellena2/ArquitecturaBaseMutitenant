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

    public static PlatformAssistantMessage Create(Guid conversationId, string role, string status, string? text, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A conversation is required.", nameof(conversationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new PlatformAssistantMessage { ConversationId = conversationId, Role = role, Status = status, Text = text, CreatedAtUtc = nowUtc };
    }
}
