using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Intervalo civil técnico inmutable, sin datos privados de la organización.</summary>
public sealed class AssistantBudgetMonth : Entity
{
    private AssistantBudgetMonth() { }
    public Guid OrganizationId { get; private set; }
    public DateOnly Month { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public static AssistantBudgetMonth Create(Guid organizationId, DateOnly month, string timeZoneId, DateTime startUtc, DateTime endUtc)
    {
        if (organizationId == Guid.Empty || month.Day != 1 || startUtc.Kind != DateTimeKind.Utc
            || endUtc.Kind != DateTimeKind.Utc || endUtc <= startUtc) throw new ArgumentException("A civil month interval is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        return new() { OrganizationId = organizationId, Month = month, TimeZoneId = timeZoneId, StartUtc = startUtc, EndUtc = endUtc };
    }
}
