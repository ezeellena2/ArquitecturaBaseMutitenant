namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class PlatformAssistantConversation : AssistantConversationBase
{
    private PlatformAssistantConversation() { }

    public static PlatformAssistantConversation Create(Guid ownerUserId, DateTime nowUtc)
    {
        var row = new PlatformAssistantConversation();
        row.Initialize(ownerUserId, nowUtc);
        return row;
    }
}
