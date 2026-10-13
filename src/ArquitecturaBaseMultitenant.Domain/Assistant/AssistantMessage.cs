using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantMessage : AssistantMessageBase, ITenantOwned
{
    private AssistantMessage() { }
    public Guid TenantId { get; private set; }

    public static AssistantMessage Create(Guid conversationId, string role, string status, string? text, DateTime nowUtc)
    {
        var row = new AssistantMessage();
        row.Initialize(conversationId, role, status, text, nowUtc);
        return row;
    }
}
