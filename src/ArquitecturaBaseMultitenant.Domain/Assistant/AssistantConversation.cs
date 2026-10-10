using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantConversation : Entity, ITenantOwned, IVersioned
{
    private AssistantConversation() { }
    public Guid TenantId { get; private set; }
    public uint Version { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Access Access { get; private set; }
    public string? Title { get; private set; }
    public bool IsSaved { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastActivityAtUtc { get; private set; }

    public static AssistantConversation Create(Guid ownerUserId, Access access, DateTime nowUtc)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        if (access is not (Access.Consumer or Access.Business)) throw new ArgumentOutOfRangeException(nameof(access));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new AssistantConversation { OwnerUserId = ownerUserId, Access = access, CreatedAtUtc = nowUtc, LastActivityAtUtc = nowUtc };
    }
}
