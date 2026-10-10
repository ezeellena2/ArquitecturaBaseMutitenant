using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantUsageDaily : Entity, ITenantOwned
{
    private AssistantUsageDaily() { }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly Day { get; private set; }
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public int MessageCount { get; private set; }
    public Money Cost { get; private set; } = null!;

    public static AssistantUsageDaily Create(Guid userId, DateOnly day, Money cost)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        ArgumentNullException.ThrowIfNull(cost);
        return new AssistantUsageDaily { UserId = userId, Day = day, Cost = cost };
    }
}
