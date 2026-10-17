namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Private committed binding. Content is removed on conversation deletion; the technical key survives.</summary>
public sealed class PlatformAssistantDurableOperation : AssistantDurableOperationBase
{
    private PlatformAssistantDurableOperation() { }

    public static PlatformAssistantDurableOperation Create(Guid owner, Guid key, string hash, string route,
        Guid conversationId, string kind, int statusCode, string? location, string snapshot, string? quickKey, DateTime nowUtc,
        Guid[]? subjectUserIds = null)
    {
        var row = new PlatformAssistantDurableOperation();
        row.Initialize(owner, key, hash, route, conversationId, kind, statusCode, location, snapshot, quickKey, nowUtc, subjectUserIds);
        return row;
    }
}
