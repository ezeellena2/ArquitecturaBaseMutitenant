using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantModelPrice : Entity, IGlobalSecurityAudited, IVersioned
{
    private AssistantModelPrice() { }
    SecurityEventType IGlobalSecurityAudited.SecurityEventType => SecurityEventType.PlatformSettingsChanged;
    Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => null;
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderModelId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public Money InputPrice { get; private set; } = null!;
    public Money OutputPrice { get; private set; } = null!;

    public static AssistantModelPrice Create(string provider, string providerModelId, string displayName, Money inputPrice, Money outputPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(inputPrice);
        ArgumentNullException.ThrowIfNull(outputPrice);
        return new AssistantModelPrice { Provider = provider, ProviderModelId = providerModelId, DisplayName = displayName,
            InputPrice = new Money(inputPrice.Amount, inputPrice.Currency), OutputPrice = new Money(outputPrice.Amount, outputPrice.Currency) };
    }
}
