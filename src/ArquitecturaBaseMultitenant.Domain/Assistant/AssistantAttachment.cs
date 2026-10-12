using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantAttachment : Entity, ITenantOwned, IVersioned
{
    private AssistantAttachment() { }
    public Guid TenantId { get; private set; }
    public uint Version { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? MessageId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public void Bind(Guid messageId)
    {
        if (messageId == Guid.Empty || MessageId is not null) throw new InvalidOperationException("An attachment can be bound once.");
        MessageId = messageId;
    }

    public static AssistantAttachment Create(Guid conversationId, string fileName, string contentType, string storageKey, long size, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A conversation is required.", nameof(conversationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new AssistantAttachment { ConversationId = conversationId, FileName = fileName, ContentType = contentType, StorageKey = storageKey, Size = size, CreatedAtUtc = nowUtc };
    }
}
