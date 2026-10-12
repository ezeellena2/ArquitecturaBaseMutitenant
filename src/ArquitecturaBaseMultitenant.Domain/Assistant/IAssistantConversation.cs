using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public interface IAssistantConversation : IVersioned
{
    Guid Id { get; }
    Guid OwnerUserId { get; }
    string? Title { get; }
    bool IsSaved { get; }
    bool IsTitleManual { get; }
    DateTime CreatedAtUtc { get; }
    DateTime LastActivityAtUtc { get; }
    void Touch(DateTime nowUtc);
    bool TrySetAutomaticTitle(string title, uint expectedVersion, DateTime nowUtc);
    void Update(string? title, bool isSaved, DateTime nowUtc);
    void RecordQuick(string title, DateTime nowUtc);
}
