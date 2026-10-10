using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantPlatformSettings : Entity, IGlobalSecurityAudited, IVersioned
{
    private AssistantPlatformSettings() : base(SingletonId) { }
    SecurityEventType IGlobalSecurityAudited.SecurityEventType => SecurityEventType.PlatformSettingsChanged;
    Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => null;
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public const string SingletonIdValue = "00000000-0000-0000-0000-000000000002";
    public static readonly Guid SingletonId = new(SingletonIdValue);
    public bool PersonalEnabled { get; private set; }
    public Guid DefaultModelId { get; private set; }
    public Guid[] AllowedModelIds { get; private set; } = [];
    public string Effort { get; private set; } = "Medium";
    public int DailyMessageLimit { get; private set; } = AssistantFeature.DefaultDailyMessageLimit;
    public int RetentionDays { get; private set; } = AssistantFeature.DefaultRetentionDays;
    public Money MonthlyOrganizationBudget { get; private set; } = null!;
    [NotAudited] public byte[]? EncryptedApiKey { get; private set; }
    [NotAudited] public string? MaskedKeySuffix { get; private set; }
    public bool HasKeyOverride { get; private set; }

    public void Update(Guid defaultModelId, Guid[] allowedModelIds, string effort, int dailyMessageLimit,
        Money monthlyOrganizationBudget, bool personalEnabled, int retentionDays)
    {
        DefaultModelId = defaultModelId;
        AllowedModelIds = [.. allowedModelIds];
        Effort = effort;
        DailyMessageLimit = dailyMessageLimit;
        MonthlyOrganizationBudget = new Money(monthlyOrganizationBudget.Amount, monthlyOrganizationBudget.Currency);
        PersonalEnabled = personalEnabled;
        RetentionDays = retentionDays;
    }

    public void ReplaceKey(byte[]? encryptedKey, string? suffix)
    {
        HasKeyOverride = true;
        EncryptedApiKey = encryptedKey?.ToArray();
        MaskedKeySuffix = suffix;
    }

    public static AssistantPlatformSettings Create(bool personalEnabled, Money monthlyOrganizationBudget)
    {
        ArgumentNullException.ThrowIfNull(monthlyOrganizationBudget);
        return new AssistantPlatformSettings { PersonalEnabled = personalEnabled, MonthlyOrganizationBudget = monthlyOrganizationBudget };
    }
}
