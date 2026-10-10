using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Cuota técnica global de una cuenta, compartida por todos sus accesos.</summary>
public sealed class AssistantPlatformKeyDailyQuota : Entity
{
    private AssistantPlatformKeyDailyQuota() { }
    public Guid UserId { get; private set; }
    public DateOnly DayUtc { get; private set; }
    public int ReservedQueries { get; private set; }
    public int ConsumedQueries { get; private set; }
    public static AssistantPlatformKeyDailyQuota Create(Guid userId, DateOnly dayUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        return new() { UserId = userId, DayUtc = dayUtc };
    }
    public bool CanReserve(int limit) => (long)ReservedQueries + ConsumedQueries < limit;
    public void Reserve() => ReservedQueries = checked(ReservedQueries + 1);
    public void Release()
    {
        if (ReservedQueries < 1) throw new InvalidOperationException("The operation has no query reservation.");
        ReservedQueries--;
    }
    public void Consume() { Release(); ConsumedQueries = checked(ConsumedQueries + 1); }
}
