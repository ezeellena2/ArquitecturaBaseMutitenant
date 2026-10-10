using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class PlatformAssistantConversation : Entity, IVersioned
{
    private PlatformAssistantConversation() { }
    public uint Version { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string? Title { get; private set; }
    public bool IsSaved { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastActivityAtUtc { get; private set; }

    public static PlatformAssistantConversation Create(Guid ownerUserId, DateTime nowUtc)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new PlatformAssistantConversation { OwnerUserId = ownerUserId, CreatedAtUtc = nowUtc, LastActivityAtUtc = nowUtc };
    }
}
