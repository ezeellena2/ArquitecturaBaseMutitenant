namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class PlatformAssistantMessage : AssistantMessageBase
{
    private PlatformAssistantMessage() { }

    public static PlatformAssistantMessage Create(Guid conversationId, string role, string status, string? text, DateTime nowUtc)
    {
        var row = new PlatformAssistantMessage();
        row.Initialize(conversationId, role, status, text, nowUtc);
        return row;
    }
}
