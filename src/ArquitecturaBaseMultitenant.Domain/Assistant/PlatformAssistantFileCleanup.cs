using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class PlatformAssistantFileCleanup : Entity
{
    private PlatformAssistantFileCleanup() { }
    private PlatformAssistantFileCleanup(Guid attachmentId) : base(attachmentId) { }
    public Guid OwnerUserId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public static PlatformAssistantFileCleanup Create(Guid attachmentId, Guid owner, string storageKey, DateTime nowUtc)
    {
        if (owner == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(owner));
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new(attachmentId) { OwnerUserId = owner, StorageKey = storageKey, CreatedAtUtc = nowUtc };
    }
}
