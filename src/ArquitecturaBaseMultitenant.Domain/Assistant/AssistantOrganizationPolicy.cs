using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantOrganizationPolicy : Entity, IGlobalSecurityAudited, IVersioned
{
    private AssistantOrganizationPolicy() { }
    SecurityEventType IGlobalSecurityAudited.SecurityEventType => SecurityEventType.TenantFeaturesChanged;
    Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => OrganizationId;
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public Guid OrganizationId { get; private set; }
    public bool AllowOwnKey { get; private set; }
    public int DailyMessageLimit { get; private set; } = AssistantFeature.DefaultDailyMessageLimit;
    public Money MonthlyBudget { get; private set; } = null!;
    public int RetentionDays { get; private set; } = AssistantFeature.DefaultRetentionDays;
    public void SetAllowOwnKey(bool allowOwnKey) => AllowOwnKey = allowOwnKey;

    public static AssistantOrganizationPolicy Create(Guid organizationId, Money monthlyBudget)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("An organization is required.", nameof(organizationId));
        ArgumentNullException.ThrowIfNull(monthlyBudget);
        return new AssistantOrganizationPolicy { OrganizationId = organizationId, MonthlyBudget = monthlyBudget };
    }
}
