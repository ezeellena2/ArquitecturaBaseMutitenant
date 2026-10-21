using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class PlatformAssistantConversationActivity : AssistantConversationActivityBase
{
    private PlatformAssistantConversationActivity() { }
    private PlatformAssistantConversationActivity(Guid conversationId, Guid ownerUserId, DateTime nowUtc)
        : base(conversationId, ownerUserId, Access.Platform, nowUtc) { }

    public static PlatformAssistantConversationActivity Create(Guid conversationId, Guid ownerUserId, DateTime nowUtc) =>
        new(conversationId, ownerUserId, nowUtc);
}
