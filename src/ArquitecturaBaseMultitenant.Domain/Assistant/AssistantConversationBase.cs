using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public abstract class AssistantConversationBase : Entity, IAssistantConversation
{
    public uint Version { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string? Title { get; private set; }
    public bool IsSaved { get; private set; }
    public bool IsTitleManual { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastActivityAtUtc { get; private set; }

    public void Touch(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        LastActivityAtUtc = nowUtc;
    }
    public bool TrySetAutomaticTitle(string title, uint expectedVersion, DateTime nowUtc)
    {
        if (IsTitleManual || Title is not null || Version != expectedVersion) return false;
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > TextLimits.AssistantTitle) throw new ArgumentException("The title is too long.", nameof(title));
        Touch(nowUtc); Title = title;
        return true;
    }
    public void Update(string? title, bool isSaved, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        if (title is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            if (title.Length > TextLimits.AssistantTitle) throw new ArgumentException("The title is too long.", nameof(title));
            Title = title;
            IsTitleManual = true;
            LastActivityAtUtc = nowUtc;
        }
        IsSaved = isSaved;
    }

    public void RecordQuick(string title, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > TextLimits.AssistantTitle) throw new ArgumentException("The title is too long.", nameof(title));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        if (!IsTitleManual && Title is null) Title = title;
        LastActivityAtUtc = nowUtc;
    }

    protected void Initialize(Guid ownerUserId, DateTime nowUtc)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        OwnerUserId = ownerUserId; CreatedAtUtc = nowUtc; LastActivityAtUtc = nowUtc;
    }
}
