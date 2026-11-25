using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>
/// Cupo diario con la clave de la plataforma de una persona en una organización, o fuera de una (<see cref="OrganizationId"/>
/// nula: el acceso personal y Plataforma). Desde la 271 el día es el civil de la zona de la organización, o de la del perfil
/// fuera de una, aunque la columna conserve su nombre: una persona en dos organizaciones tiene dos cupos.
/// </summary>
public sealed class AssistantPlatformKeyDailyQuota : Entity
{
    private AssistantPlatformKeyDailyQuota() { }
    public Guid UserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public DateOnly DayUtc { get; private set; }
    public int ReservedQueries { get; private set; }
    public int ConsumedQueries { get; private set; }
    public static AssistantPlatformKeyDailyQuota Create(Guid userId, Guid? organizationId, DateOnly dayUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        if (organizationId == Guid.Empty) throw new ArgumentException("An organization or none is required.", nameof(organizationId));
        return new() { UserId = userId, OrganizationId = organizationId, DayUtc = dayUtc };
    }
    public bool CanReserve(int limit) => AssistantDailyQuotaRule.CanReserve(ReservedQueries, ConsumedQueries, limit);
    public void Reserve() => ReservedQueries = checked(ReservedQueries + 1);
    public void Release()
    {
        if (ReservedQueries < 1) throw new InvalidOperationException("The operation has no query reservation.");
        ReservedQueries--;
    }
    public void Consume() { Release(); ConsumedQueries = checked(ConsumedQueries + 1); }
}
