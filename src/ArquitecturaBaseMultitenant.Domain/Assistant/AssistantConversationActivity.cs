using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantConversationActivity : AssistantConversationActivityBase, ITenantOwned
{
    private AssistantConversationActivity() { }
    private AssistantConversationActivity(Guid conversationId, Guid ownerUserId, Access access, DateTime nowUtc)
        : base(conversationId, ownerUserId, access, nowUtc) { }

    public Guid TenantId { get; private set; }

    public static AssistantConversationActivity Create(Guid conversationId, Guid ownerUserId, Access access, DateTime nowUtc)
    {
        if (access is not (Access.Consumer or Access.Business)) throw new ArgumentOutOfRangeException(nameof(access));
        return new AssistantConversationActivity(conversationId, ownerUserId, access, nowUtc);
    }
}
