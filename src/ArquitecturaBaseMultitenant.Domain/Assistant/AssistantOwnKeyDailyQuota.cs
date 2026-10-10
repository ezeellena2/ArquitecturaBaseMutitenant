using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Cuota independiente de clave propia, sellada por el tenant del llamador.</summary>
public sealed class AssistantOwnKeyDailyQuota : Entity, ITenantOwned
{
    private AssistantOwnKeyDailyQuota() { }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly DayUtc { get; private set; }
    public int ReservedQueries { get; private set; }
    public int ConsumedQueries { get; private set; }
    public static AssistantOwnKeyDailyQuota Create(Guid userId, DateOnly dayUtc)
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
