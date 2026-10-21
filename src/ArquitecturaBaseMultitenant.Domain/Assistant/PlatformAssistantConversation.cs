namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class PlatformAssistantConversation : AssistantConversationBase
{
    private PlatformAssistantConversation() { }
    public PlatformAssistantConversationActivity Activity { get; private set; } = null!;
    protected override AssistantConversationActivityBase ActivityState => Activity;

    public static PlatformAssistantConversation Create(Guid ownerUserId, DateTime nowUtc)
    {
        var row = new PlatformAssistantConversation();
        row.Initialize(ownerUserId, nowUtc);
        row.Activity = PlatformAssistantConversationActivity.Create(row.Id, ownerUserId, nowUtc);
        return row;
    }
}
