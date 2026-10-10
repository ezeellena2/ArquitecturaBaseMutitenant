using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantOrganizationSettings : Entity, ITenantOwned, IAuditable, IVersioned
{
    private AssistantOrganizationSettings() { }
    public Guid TenantId { get; private set; }
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public string Mode { get; private set; } = "PlatformKey";
    public Guid ModelId { get; private set; }
    public string Effort { get; private set; } = "Medium";
    public int DailyMessageLimit { get; private set; } = AssistantFeature.DefaultDailyMessageLimit;
    public int RetentionDays { get; private set; } = AssistantFeature.DefaultRetentionDays;
    public Money? MonthlyBudget { get; private set; }
    [NotAudited] public byte[]? EncryptedApiKey { get; private set; }
    [NotAudited] public string? MaskedKeySuffix { get; private set; }

    public static AssistantOrganizationSettings Create() => new();
}
