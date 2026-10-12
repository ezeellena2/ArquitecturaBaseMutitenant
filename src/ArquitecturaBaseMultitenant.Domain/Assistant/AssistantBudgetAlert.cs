using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Control técnico de entrega: no guarda destinatarios ni contenido de conversaciones.</summary>
public sealed class AssistantBudgetAlert : Entity
{
    private AssistantBudgetAlert() { }
    public Guid OrganizationId { get; private set; }
    public Guid MonthId { get; private set; }
    public AssistantKeyFunding Funding { get; private set; }
    public int Threshold { get; private set; }
    public Money Used { get; private set; } = null!;
    public Money Budget { get; private set; } = null!;
    public DateTime? EnqueuedAtUtc { get; private set; }
    public static AssistantBudgetAlert Create(Guid organizationId, Guid monthId, AssistantKeyFunding funding,
        int threshold, Money used, Money budget)
    {
        if (organizationId == Guid.Empty || monthId == Guid.Empty || threshold is not (80 or 100)
            || !Enum.IsDefined(funding) || budget.Amount <= 0 || used.Amount < 0 || !used.Currency.Equals(budget.Currency))
            throw new ArgumentException("A reached monthly threshold is required.");
        return new() { OrganizationId = organizationId, MonthId = monthId, Funding = funding, Threshold = threshold, Used = used, Budget = budget };
    }
    public void MarkEnqueued(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        EnqueuedAtUtc ??= nowUtc;
    }
}
