namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class PlatformAssistantTurn : AssistantTurnBase
{
    private PlatformAssistantTurn() { }

    public static PlatformAssistantTurn Create(Guid conversationId, DateTime nowUtc)
    {
        var row = new PlatformAssistantTurn();
        row.Initialize(conversationId, nowUtc);
        return row;
    }
}
