using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantTurn : AssistantTurnBase, ITenantOwned
{
    private AssistantTurn() { }
    public Guid TenantId { get; private set; }

    public static AssistantTurn Create(Guid conversationId, DateTime nowUtc)
    {
        var row = new AssistantTurn();
        row.Initialize(conversationId, nowUtc);
        return row;
    }
}
