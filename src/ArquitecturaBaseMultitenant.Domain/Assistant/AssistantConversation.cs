using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantConversation : AssistantConversationBase, ITenantOwned
{
    private AssistantConversation() { }
    public Guid TenantId { get; private set; }
    public Access Access { get; private set; }

    public static AssistantConversation Create(Guid ownerUserId, Access access, DateTime nowUtc)
    {
        if (access is not (Access.Consumer or Access.Business)) throw new ArgumentOutOfRangeException(nameof(access));
        var row = new AssistantConversation { Access = access };
        row.Initialize(ownerUserId, nowUtc);
        return row;
    }
}
