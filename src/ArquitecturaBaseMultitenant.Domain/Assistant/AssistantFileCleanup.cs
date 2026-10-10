using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Pending physical cleanup. It retains only a technical storage key, never a file name or bytes.</summary>
public sealed class AssistantFileCleanup : Entity, ITenantOwned
{
    private AssistantFileCleanup() { }
    private AssistantFileCleanup(Guid attachmentId) : base(attachmentId) { }
    public Guid TenantId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public static AssistantFileCleanup Create(Guid attachmentId, Guid owner, string storageKey, DateTime nowUtc)
    {
        if (owner == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(owner));
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new(attachmentId) { OwnerUserId = owner, StorageKey = storageKey, CreatedAtUtc = nowUtc };
    }
}
